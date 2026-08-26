<#
.SYNOPSIS
    XTimelineViewer のリリース用にバージョンを更新する（+ 任意でローカル検証用 ZIP を生成）。

.DESCRIPTION
    .github/workflows/release.yml は v* タグの push をトリガーに、テスト済みの未署名成果物を生成し、
    未署名であることの明示、SHA-256、GitHub Artifact Attestationを付けてGitHub Releaseへ公開する。
    SignPath承認後は、同じ公開ゲートへ署名とタイムスタンプの検証を追加する。
    本スクリプトは CI を起動する前のバージョン更新
    （csproj / Package.appxmanifest / Inno Setup / ランチャー）を担当する。
    Microsoft Store提出物はPartner CenterのIdentity取得後に別工程で生成する。
    このスクリプトはStore提出物を生成しない。

.PARAMETER Version
    新しいバージョン（例 1.9.1）。指定すると csproj と appxmanifest を更新する。
    省略時は csproj の現在値を使う（ファイルは変更しない）。

.PARAMETER WithZip
    ローカル検証用に GitHub 配布相当の ZIP（自己完結・アンパッケージド）を生成する。
    本番の GitHub リリース ZIP は CI が作るため通常は不要。

.EXAMPLE
    .\Release.ps1 -Version 1.9.1           # バージョン更新のみ
    .\Release.ps1 -Version 1.9.1 -WithZip  # 更新 + ローカル検証用 ZIP も生成
    .\Release.ps1 -WithZip                 # 現行バージョンのまま ZIP を生成
#>
[CmdletBinding()]
param(
    [string] $Version,
    [switch] $WithZip
)

$ErrorActionPreference = 'Stop'
$root    = $PSScriptRoot
$proj    = Join-Path $root 'XTimelineViewer.csproj'
$manifest= Join-Path $root 'Package.appxmanifest'
$installer = Join-Path $root 'scripts\installer.iss'
$launcherResource = Join-Path $root 'tools\launcher\xtv.rc'
$outDir  = Join-Path $root 'publish\release'

function Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }

# ── バージョン解決（必要なら更新） ──────────────────────────────────────────────
Step 'バージョン'
if ($Version) {
    if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version は x.y.z 形式で指定してください: $Version" }
    $version4 = "$Version.0"
    (Get-Content $proj -Raw) `
        -replace '<Version>[\d.]+</Version>', "<Version>$Version</Version>" `
        -replace '<AssemblyVersion>[\d.]+</AssemblyVersion>', "<AssemblyVersion>$version4</AssemblyVersion>" `
        -replace '<FileVersion>[\d.]+</FileVersion>', "<FileVersion>$version4</FileVersion>" `
        -replace '<InformationalVersion>[\d.]+</InformationalVersion>', "<InformationalVersion>$Version</InformationalVersion>" |
        Set-Content $proj -Encoding utf8 -NoNewline
    # Identity の Version のみを置換する。-creplace（大文字小文字を区別）で XML 宣言の
    # 小文字 version="1.0" を除外し、ProcessorArchitecture への先読みで MinVersion 等を除外する。
    (Get-Content $manifest -Raw) -creplace 'Version="[\d.]+"(?=\s+ProcessorArchitecture)', "Version=`"$Version.0`"" |
        Set-Content $manifest -Encoding utf8 -NoNewline
    (Get-Content $installer -Raw) -replace '#define MyAppVersion "[\d.]+"', "#define MyAppVersion `"$Version`"" |
        Set-Content $installer -Encoding utf8 -NoNewline

    $parts = $Version.Split('.')
    $versionComma = "$($parts[0]),$($parts[1]),$($parts[2]),0"
    (Get-Content $launcherResource -Raw) `
        -replace '#define XTV_VERSION_COMMA [\d,]+', "#define XTV_VERSION_COMMA $versionComma" `
        -replace '#define XTV_VERSION_STRING "[\d.]+"', "#define XTV_VERSION_STRING `"$Version`"" |
        Set-Content $launcherResource -Encoding utf8 -NoNewline

    Write-Host "csproj / appxmanifest / installer / launcher を $Version に更新しました"
} else {
    if ((Get-Content $proj -Raw) -match '<Version>([\d.]+)</Version>') { $Version = $Matches[1] }
    else { throw 'csproj から Version を読み取れませんでした' }
}
Write-Host "対象バージョン: $Version"

# ── ローカル検証用 ZIP（自己完結・アンパッケージド。本番は CI が生成） ─────────────
if ($WithZip) {
    Remove-Item $outDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force $outDir | Out-Null
    # 実行中インスタンスがあるとビルドがファイルロックで失敗するため止める
    try { Stop-Process -Name XTimelineViewer -Force -ErrorAction Stop } catch {}

    dotnet restore $proj --locked-mode --nologo
    if ($LASTEXITCODE -ne 0) { throw 'locked restore 失敗' }

    foreach ($rid in 'win-x64', 'win-arm64') {
        $plat = if ($rid -eq 'win-x64') { 'x64' } else { 'arm64' }
        Step "ZIP 発行: $rid"
        $pubDir = Join-Path $root "publish\$rid"
        # EffectivePlatform を明示しないと、WebView2 SDK がビルドホストの RID(win-x64) を見て
        # x64 の Microsoft.Web.WebView2.Core.dll を arm64 出力に混入させ、arm64 で BadImageFormat になる（#267）。
        dotnet publish $proj -c Release -r $rid -p:PlatformTarget=$plat -p:EffectivePlatform=$plat -p:WindowsPackageType=None --no-restore -o $pubDir
        if ($LASTEXITCODE -ne 0) { throw "publish 失敗: $rid" }
        # コマンドライン起動用ランチャーは CI でソースからビルドする。
        # ローカル ZIP では、同じアーキテクチャで事前ビルドされた検証用成果物だけを使う。
        $launcher = Join-Path $root "tools\launcher\build\$plat\xtv.exe"
        if (-not (Test-Path $launcher)) {
            throw "ランチャーがありません。先に tools\launcher\build-launcher.ps1 -Architecture $plat を実行してください: $launcher"
        }
        Copy-Item $launcher (Join-Path $pubDir 'xtv.exe') -Force
        & (Join-Path $root 'scripts\collect-legal-notices.ps1') -Destination "publish\$rid"
        if ($LASTEXITCODE -ne 0) { throw "ライセンス収集失敗: $rid" }
        $zip = Join-Path $outDir "XTimelineViewer-Kotsume-v$Version-$rid-Portable.zip"
        Compress-Archive -Path "$pubDir\*" -DestinationPath $zip -Force
        Write-Host "→ $zip"
    }

    Step '完了：成果物'
    Get-ChildItem $outDir | Select-Object Name, @{N='MB';E={[math]::Round($_.Length/1MB,1)}} | Format-Table -AutoSize
}

Write-Host @"
次の手順（手動）:
  1. バージョン更新分をコミット & PR → main にマージ
  2. mainでRelease workflowを手動実行し、公開なしで全工程を検証する
  3. main上のコミットへ v$Version タグを付けてpushする
     → CIが未署名のZIPとインストーラーを生成し、注意書き、SHA-256、来歴証明とともにReleaseを公開する
  4. SignPath承認後の版では、第一者バイナリとインストーラーの署名・タイムスタンプ検証を公開ゲートへ追加する
"@ -ForegroundColor Yellow
