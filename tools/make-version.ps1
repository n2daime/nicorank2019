# アプリ自動更新の version.json 生成スクリプト（Issue #47・47.2）
#
# なぜスクリプト化するか：version.json の tag・version・url・sha256・size はすべて機械から取れる値か
# リリース時の確定値であり、人間が写すと URL の欠けやハッシュの取り違えが起きる。
# 機械が書いて機械（AppUpdateChecker・nicorankUpdater）が読むことで、運用負荷を増やさず検証を強くする。
# URL の組み立てはしない（引数で完全 URL を渡す）。updater 側に組み立てロジックを持たせない方針と同一で、
# 将来の命名変更で更新機構自体が壊れないようにするためである。
#
# 使い方（例）：
#   powershell -ExecutionPolicy Bypass -File tools\make-version.ps1 `
#     -ZipPath "nicorank2019.zip" `
#     -Tag "v20261004_nicorank" `
#     -Version "2026.10.04" `
#     -Url "https://github.com/n2daime/nicorank2019/releases/download/v20261004_nicorank/nicorank2019.zip"
#
# 出力は -OutDir（省略時はスクリプト自身の場所）に version.json として作る。
# NAS への配置は人間が行う（エージェントは NAS に触れない）。配置手順は docs/knowledge/release.md を参照。
# 配布物（GitHub Release の zip）には含めない。version.json は NAS 専用である。

[CmdletBinding()]
param(
    # 配布 zip（固定名。ホワイトリスト方式で作った実物）。size・sha256 を実測する。
    [Parameter(Mandatory = $true)]
    [string]$ZipPath,
    # リリースタグ（例：v20261004_nicorank）。version.json の tag・notes の既定値に使う。
    [Parameter(Mandatory = $true)]
    [string]$Tag,
    # 配布版数（例：2026.10.04）。System.Version で読める数値形式でなければならない。
    # タグ文字列の大小比較は将来の命名変更で壊れるため、比較はこの項目だけに寄せている。
    [Parameter(Mandatory = $true)]
    [string]$Version,
    # 配布 zip の完全 URL（GitHub Release の asset）。組み立てずそのまま書く。
    [Parameter(Mandatory = $true)]
    [string]$Url,
    # 更新案内ダイアログの「詳細」リンク先。省略時はリリースタグのページにする。
    [string]$Notes = "",
    # version.json の apps 内キー。今回は nicorank2019 のみ。
    [string]$AppKey = "nicorank2019",
    # 出力先フォルダ。省略時はスクリプト自身の場所（探す手間と取り違えをなくすため）。
    [string]$OutDir = $PSScriptRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) {
    throw "配布 zip が見つかりません: $ZipPath"
}
try {
    # 版数はクライアントが System.Version 比較するため、ここで読めることを保証する。
    # 読めない版数を配ると全クライアントの判定が Unknown 側に倒れる。
    $parsedVersion = New-Object System.Version($Version.Trim())
} catch {
    throw "版数が数値形式ではありません: $Version"
}
$uri = $null
if (-not [System.Uri]::TryCreate($Url, [System.UriKind]::Absolute, [ref]$uri)) {
    throw "配布 URL が完全 URL ではありません: $Url"
}
if ($uri.Scheme -ne "http" -and $uri.Scheme -ne "https") {
    throw "配布 URL は http/https にしてください: $Url"
}
if ([string]::IsNullOrWhiteSpace($Notes)) {
    $Notes = "https://github.com/n2daime/nicorank2019/releases/tag/$Tag"
}

$item = Get-Item -LiteralPath $ZipPath
if ($item.Length -le 0) {
    throw "配布 zip のサイズが 0 です: $ZipPath"
}
$hash = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Output ("計測: {0} ({1} bytes, sha256={2}...)" -f $item.Name, $item.Length, $hash.Substring(0, 16))

$entry = [ordered]@{
    tag     = $Tag
    version = $Version.Trim()
    url     = $Url
    sha256  = $hash
    size    = $item.Length
    notes   = $Notes
}
$manifest = [ordered]@{
    schema = 1
    apps   = [ordered]@{ $AppKey = $entry }
}
$json = ($manifest | ConvertTo-Json -Depth 5)
if (-not (Test-Path -LiteralPath $OutDir -PathType Container)) {
    New-Item $OutDir -ItemType Directory | Out-Null
}
$manifestPath = Join-Path $OutDir "version.json"
# UTF-8（BOM なし）で書く。アプリ側の JsonConvert が BOM 付きでも読めるが、
# ブラウザ確認や差分比較での余計な差を避けるためである（make-baseline.ps1 と同一）。
[System.IO.File]::WriteAllText($manifestPath, $json + [Environment]::NewLine, (New-Object System.Text.UTF8Encoding($false)))
Write-Output ("version.json を更新: {0}" -f $manifestPath)
Write-Output "次に version.json を NAS の公開配下へ配置してください（手順は docs/knowledge/release.md）。"
