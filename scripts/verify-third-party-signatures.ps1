[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Directory
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$root = if ([System.IO.Path]::IsPathRooted($Directory)) {
    [System.IO.Path]::GetFullPath($Directory)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Directory))
}

if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw "Publish directory was not found: $root"
}

$firstPartyNames = @("XTimelineViewer.exe", "XTimelineViewer.dll", "xtv.exe")
$signed = 0
$unsigned = 0

foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
    $_.Extension -in @(".exe", ".dll") -and $_.Name -notin $firstPartyNames
}) {
    $signature = Get-AuthenticodeSignature -LiteralPath $file.FullName
    switch ($signature.Status) {
        "Valid" { $signed++; break }
        "NotSigned" { $unsigned++; break }
        default {
            throw "Existing third-party signature is not valid: $($file.FullName) ($($signature.Status))"
        }
    }
}

Write-Host "Third-party signature check completed: valid=$signed unsigned=$unsigned directory=$root"
