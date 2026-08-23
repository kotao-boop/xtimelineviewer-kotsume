[CmdletBinding()]
param(
    [string]$X64Directory = "publish/x64",
    [string]$Arm64Directory = "publish/arm64",
    [Parameter(Mandatory = $true)]
    [string]$InstallerPath
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$expectedProduct = "XTimelineViewer Kotsume Edition"

function Resolve-RepoPath([string]$value) {
    if ([System.IO.Path]::IsPathRooted($value)) {
        return [System.IO.Path]::GetFullPath($value)
    }
    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $value))
}

$files = @(
    (Join-Path (Resolve-RepoPath $X64Directory) "XTimelineViewer.exe"),
    (Join-Path (Resolve-RepoPath $X64Directory) "XTimelineViewer.dll"),
    (Join-Path (Resolve-RepoPath $X64Directory) "xtv.exe"),
    (Join-Path (Resolve-RepoPath $Arm64Directory) "XTimelineViewer.exe"),
    (Join-Path (Resolve-RepoPath $Arm64Directory) "XTimelineViewer.dll"),
    (Join-Path (Resolve-RepoPath $Arm64Directory) "xtv.exe"),
    (Resolve-RepoPath $InstallerPath)
)

foreach ($file in $files) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Required signed artifact was not found: $file"
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $file
    if ($signature.Status -ne "Valid") {
        throw "Authenticode signature is not valid: $file ($($signature.Status))"
    }
    if (-not $signature.SignerCertificate) {
        throw "Signer certificate is missing: $file"
    }
    if (-not $signature.TimeStamperCertificate) {
        throw "Trusted timestamp is missing: $file"
    }

    if ([System.IO.Path]::GetFileName($file) -in @("XTimelineViewer.exe", "xtv.exe")) {
        $productName = (Get-Item -LiteralPath $file).VersionInfo.ProductName
        if ($productName -ne $expectedProduct) {
            throw "Unexpected ProductName after signing: $file ($productName)"
        }
    }

    Write-Host "Valid signature: $file"
}
