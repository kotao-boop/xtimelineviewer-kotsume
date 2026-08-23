<#
.SYNOPSIS
    xtv.exe を公開ソースから再現可能な手順でビルドする。

.PARAMETER Architecture
    x64 または arm64。arm64 は x64 ホストからクロスコンパイルする。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('x64', 'arm64')]
    [string] $Architecture
)

$ErrorActionPreference = 'Stop'

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) {
    $vswhereCommand = Get-Command vswhere.exe -ErrorAction SilentlyContinue
    if ($vswhereCommand) { $vswhere = $vswhereCommand.Source }
}
if (-not (Test-Path -LiteralPath $vswhere)) {
    throw 'vswhere.exe が見つかりません。Visual Studio C++ Build Tools をインストールしてください。'
}

$requirements = @('Microsoft.VisualStudio.Component.VC.Tools.x86.x64')
if ($Architecture -eq 'arm64') {
    $requirements += 'Microsoft.VisualStudio.Component.VC.Tools.ARM64'
}
$vswhereArguments = @('-latest', '-products', '*')
foreach ($requirement in $requirements) {
    $vswhereArguments += @('-requires', $requirement)
}
$vswhereArguments += @('-property', 'installationPath')
$installationPath = & $vswhere @vswhereArguments
if ($LASTEXITCODE -ne 0 -or -not $installationPath) {
    throw "必要なC++ Build Toolsを含むVisual Studioが見つかりません: $($requirements -join ', ')"
}

$vsDevCmd = Join-Path $installationPath 'Common7\Tools\VsDevCmd.bat'
if (-not (Test-Path -LiteralPath $vsDevCmd)) {
    throw "VsDevCmd.bat が見つかりません: $vsDevCmd"
}

# VsDevCmd.bat が設定する環境変数を現在のPowerShellへ取り込む。
$developerCommand = 'call "{0}" -no_logo -arch={1} -host_arch=x64 >nul && set' -f `
    $vsDevCmd, $Architecture
$environmentLines = & $env:ComSpec /d /s /c $developerCommand
if ($LASTEXITCODE -ne 0) {
    throw "Visual Studio 開発環境の初期化に失敗しました: $Architecture"
}
foreach ($line in $environmentLines) {
    $separator = $line.IndexOf('=')
    if ($separator -le 0) { continue }
    $name = $line.Substring(0, $separator)
    $value = $line.Substring($separator + 1)
    [Environment]::SetEnvironmentVariable($name, $value, 'Process')
}

$outputDir = Join-Path $PSScriptRoot "build\$Architecture"
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

$resourcePath = Join-Path $outputDir 'xtv.res'
$objectPath = Join-Path $outputDir 'xtv.obj'
$exePath = Join-Path $outputDir 'xtv.exe'

Push-Location $PSScriptRoot
try {
    & rc.exe /nologo /i $PSScriptRoot /fo $resourcePath xtv.rc
    if ($LASTEXITCODE -ne 0) { throw "rc.exe が失敗しました: $LASTEXITCODE" }

    & cl.exe /nologo /utf-8 /O1 /MT /EHsc /DUNICODE /D_UNICODE `
        "/Fo$objectPath" xtv.cpp $resourcePath "/Fe$exePath" `
        /link /SUBSYSTEM:WINDOWS Shell32.lib
    if ($LASTEXITCODE -ne 0) { throw "cl.exe が失敗しました: $LASTEXITCODE" }
}
finally {
    Pop-Location
}

$version = (Get-Item -LiteralPath $exePath).VersionInfo
Write-Host "Built: $exePath" -ForegroundColor Green
Write-Host "Architecture: $Architecture"
Write-Host "Product: $($version.ProductName) $($version.ProductVersion)"
