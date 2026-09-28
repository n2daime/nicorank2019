# ベースラインDB配布物の作成スクリプト（Issue #36）
#
# なぜスクリプト化するか：baseline.json の日付・ファイル名・サイズ・sha256 はすべて機械から取れる値であり、
# 人間が写すとサイズの桁やハッシュの取り違えが起きる。機械が書いて機械（BaselineDownloader）が読むことで、
# 運用負荷を増やさずに検証を強くする（sha256 必須）。
#
# 置き場所：このスクリプトは配布フォルダ（例：\\ds224\web\nicorank\baseline）に置いたまま使う。
# なぜ配布フォルダに置くか：PC のどこに置いたかを探す手間をなくすためである。
# 原本はリポジトリの tools/make-baseline.ps1 にあり、内容を変えたら配布フォルダのコピーに上書きする。
#
# 使い方（例）：
#   powershell -ExecutionPolicy Bypass -File \\ds224\web\nicorank\baseline\make-baseline.ps1 `
#     -DbDir "実運用DBのあるフォルダ"
#   -DbDir を省略するとカレントフォルダを使う（DBフォルダに移動してから実行する場合）。
#
# 出力はスクリプトと同じフォルダに作る（探す手間と取り違えをなくすため）。
# zip 名は固定（LogOfficial.zip / NicoranHistory.zip）とし、改名作業はしない。
# 日付は baseline.json の中にだけ持つ。ファイル名に日付を入れないのは、改名自体が取り違えの元だからである。
# 単一最新の運用とし、世代は残さない。世代を残したい場合は人間が事前に退避する（ツールでは対応しない）。
#
# 実行前条件：集計アプリを終了させてから実行すること。
# なぜ終了させるか：DB は WAL モードで動き、直近コミットが -wal ファイルに残る場合がある。
# zip には -wal を含めないため、起動中の DB を固めると直近分が欠落し得る。終了後の静止点で固める。
#
# 配布物（PG の zip）には含めない。release.md のホワイトリスト方式を崩さないためである。

[CmdletBinding()]
param(
    # ベースライン元になる DB があるフォルダ（LogOfficial.db / NicoranHistory.db を読む）。
    # 省略時はカレントフォルダ。探す手間をなくすためである。
    # なぜ表示してから実行するか：既定で意図しないフォルダを固めないよう、対象と出力を先に明示するためである。
    [string]$DbDir = (Get-Location).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# 2GB級の圧縮では Compress-Archive の青い進捗バーが長時間出て不安になるため抑止し、
# 代わりにスクリプト側で開始と完了を1行ずつ出す。終わったかどうかが明確になる。
$ProgressPreference = 'SilentlyContinue'

# 出力先はスクリプト自身の場所にする。実行場所（カレント）に依存させないためである。
$OutDir = $PSScriptRoot
$DateStamp = (Get-Date -Format "yyyyMMdd")

# 対象はベースライン2種のみに絞る。ApiXML.db / Dailylog.db はキャッシュ扱いのため配布しない。
# なぜ2種か：20年運営の History（長期回数・門番拡張）と LastResult（前回順位）の継続性に意味があり、
# 新規生成では継続性が切れるためである（Issue #36 方針）。
$targets = @(
    @{ DbFile = "LogOfficial.db"; ZipName = "LogOfficial.zip"; Key = "logOfficial" },
    @{ DbFile = "NicoranHistory.db"; ZipName = "NicoranHistory.zip"; Key = "nicoranHistory" }
)

if (-not (Test-Path -LiteralPath $DbDir -PathType Container)) {
    throw "DB フォルダが見つかりません: $DbDir"
}

Write-Output ("対象DBフォルダ: {0}" -f $DbDir)
Write-Output ("出力先: {0}" -f $OutDir)

$entries = @{}
foreach ($t in $targets) {
    $dbPath = Join-Path $DbDir $t.DbFile
    if (-not (Test-Path -LiteralPath $dbPath -PathType Leaf)) {
        throw "ベースライン元の DB が見つかりません: $dbPath"
    }
    $zipPath = Join-Path $OutDir $t.ZipName

    # DB ごとに分離して zip 化する。一式 zip にすると部分欠損対応の中央管理者が必要になり、
    # 各 DB 所有クラスへの分離（DbMigrationCoordinator の設計）と逆方向になるためである。
    # 同名があれば上書きする（単一最新の運用。世代を残したい場合は実行前に人間が退避する）。
    # 2GB級は数十分かかるため開始を明示する（進捗バーは抑止済み）。
    Write-Output ("圧縮中です（数十分かかります）: {0} -> {1}" -f $t.DbFile, $t.ZipName)
    Compress-Archive -LiteralPath $dbPath -DestinationPath $zipPath -CompressionLevel Optimal -Force

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

Write-Output "次にブラウザで baseline.json が見えることを確認してください。"
