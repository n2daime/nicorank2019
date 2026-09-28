# ベースラインDB配布物の作成スクリプト（Issue #36）
#
# なぜスクリプト化するか：baseline.json の日付・ファイル名・サイズ・sha256 はすべて機械から取れる値であり、
# 人間が写すとサイズの桁やハッシュの取り違えが起きる。機械が書いて機械（BaselineDownloader）が読むことで、
# 運用負荷を増やさずに検証を強くする（sha256 必須）。
#
# 使い方（例）：
#   powershell -ExecutionPolicy Bypass -File tools\make-baseline.ps1 `
#     -DbDir "DB" -OutDir "\\ds224\web\nicorank\baseline" -DateStamp "20260921"
#
# 人間作業はこのスクリプトの実行と、ブラウザで baseline.json が見えることの確認だけにする。
# 古い世代の削除は自動では行わない（戻せなくなるため）。直近2〜3世代だけ残す運用は目視で行う。
# 配布物（PG の zip）には含めない。release.md のホワイトリスト方式を崩さないためである。
#
# 実行前条件：集計アプリを終了させてから実行すること。
# なぜ終了させるか：DB は WAL モードで動き、直近コミットが -wal ファイルに残る場合がある。
# zip には -wal を含めないため、起動中の DB を固めると直近分が欠落し得る。終了後の静止点で固める。
#
# 失敗時の掃除：2件目で失敗すると1件目の zip が残ったまま baseline.json は更新されず、
# 同日再実行は「同名の zip が既にあります」で止まる。この場合は残存 zip を手動で消してから再実行すること。
# 自動で消さないのは、配布物を勝手に消す方が危険なためである。

[CmdletBinding()]
param(
    # ベースライン元になる DB があるフォルダ（LogOfficial.db / NicoranHistory.db を読む）
    [string]$DbDir = "DB",
    # 配置先（NAS の Web 公開配下。置くと https://2daime.myds.me/nicorank/baseline/ で参照できる想定）
    [string]$OutDir = "\\ds224\web\nicorank\baseline",
    # 版の日付（yyyyMMdd。ファイル名と baseline.json の date になる）
    [string]$DateStamp = (Get-Date -Format "yyyyMMdd")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# 対象はベースライン2種のみに絞る。ApiXML.db / Dailylog.db はキャッシュ扱いのため配布しない。
# なぜ2種か：20年運営の History（長期回数・門番拡張）と LastResult（前回順位）の継続性に意味があり、
# 新規生成では継続性が切れるためである（Issue #36 方針）。
$targets = @(
    @{ DbFile = "LogOfficial.db"; ZipName = "LogOfficial_baseline_${DateStamp}.zip"; Key = "logOfficial" },
    @{ DbFile = "NicoranHistory.db"; ZipName = "NicoranHistory_baseline_${DateStamp}.zip"; Key = "nicoranHistory" }
)

if (-not (Test-Path -LiteralPath $DbDir -PathType Container)) {
    throw "DB フォルダが見つかりません: $DbDir"
}
if (-not (Test-Path -LiteralPath $OutDir -PathType Container)) {
    throw "配置先フォルダが見つかりません: $OutDir"
}

$entries = @{}
foreach ($t in $targets) {
    $dbPath = Join-Path $DbDir $t.DbFile
    if (-not (Test-Path -LiteralPath $dbPath -PathType Leaf)) {
        throw "ベースライン元の DB が見つかりません: $dbPath"
    }
    $zipPath = Join-Path $OutDir $t.ZipName
    if (Test-Path -LiteralPath $zipPath) {
        throw "同名の zip が既にあります（作り直し時は日付を変えるか旧版を退避してください）: $zipPath"
    }

    # DB ごとに分離して zip 化する。一式 zip にすると部分欠損対応の中央管理者が必要になり、
    # 各 DB 所有クラスへの分離（DbMigrationCoordinator の設計）と逆方向になるためである。
    Compress-Archive -LiteralPath $dbPath -DestinationPath $zipPath -CompressionLevel Optimal

    $item = Get-Item -LiteralPath $zipPath
    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $entries[$t.Key] = [ordered]@{
        file   = $t.ZipName
        date   = $DateStamp
        size   = $item.Length
        sha256 = $hash
    }
    Write-Output ("作成: {0} ({1} bytes, sha256={2}...)" -f $t.ZipName, $item.Length, $hash.Substring(0, 16))
}

# baseline.json は UTF-8（BOM なし）で書く。アプリ側の JsonConvert が BOM 付きでも読めるが、
# ブラウザ確認や差分比較での余計な差を避けるためである。
$manifest = [ordered]@{
    logOfficial    = $entries["logOfficial"]
    nicoranHistory = $entries["nicoranHistory"]
}
$json = ($manifest | ConvertTo-Json -Depth 4)
$manifestPath = Join-Path $OutDir "baseline.json"
[System.IO.File]::WriteAllText($manifestPath, $json + [Environment]::NewLine, (New-Object System.Text.UTF8Encoding($false)))
Write-Output ("baseline.json を更新: {0}" -f $manifestPath)

# 世代管理の目安を表示するだけにする。削除は人間が行う（自動削除は戻せないため）。
$existing = @(Get-ChildItem -LiteralPath $OutDir -Filter "*_baseline_*.zip" | Sort-Object Name)
Write-Output ("配置済み世代数: {0}（目安は直近2〜3世代。古い世代の削除は目視で行う）" -f $existing.Count)

Write-Output "次にブラウザで baseline.json が見えることを確認してください。"
