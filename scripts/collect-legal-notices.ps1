[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Destination,

    [string]$AssetsFile = "obj/project.assets.json"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$destinationPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Destination))
$assetsPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $AssetsFile))
$relativeDestination = [System.IO.Path]::GetRelativePath($repoRoot, $destinationPath)

if ($relativeDestination -eq "." -or
    [System.IO.Path]::IsPathRooted($relativeDestination) -or
    $relativeDestination -eq ".." -or
    $relativeDestination.StartsWith("..$([System.IO.Path]::DirectorySeparatorChar)", [StringComparison]::Ordinal) -or
    $relativeDestination.StartsWith("..$([System.IO.Path]::AltDirectorySeparatorChar)", [StringComparison]::Ordinal)) {
    throw "Destination must be a build output directory inside the repository."
}

if (-not (Test-Path -LiteralPath $assetsPath -PathType Leaf)) {
    throw "NuGet assets file was not found. Run dotnet restore first: $assetsPath"
}

$legalRoot = Join-Path $destinationPath "licenses"
if (Test-Path -LiteralPath $legalRoot -PathType Container) {
    Remove-Item -LiteralPath $legalRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $legalRoot -Force | Out-Null

Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSE") `
    -Destination (Join-Path $destinationPath "LICENSE") -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "THIRD-PARTY-NOTICES.md") `
    -Destination (Join-Path $destinationPath "THIRD-PARTY-NOTICES.md") -Force

$nugetRoot = if ($env:NUGET_PACKAGES) {
    $env:NUGET_PACKAGES
} else {
    Join-Path ([Environment]::GetFolderPath("UserProfile")) ".nuget/packages"
}

$assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json -AsHashtable
$packageKeys = @($assets.libraries.Keys | Where-Object {
    $assets.libraries[$_].type -eq "package"
} | Sort-Object)

$inventory = [System.Collections.Generic.List[string]]::new()
$inventory.Add("NuGet packages restored for this release:")

foreach ($key in $packageKeys) {
    $separator = $key.LastIndexOf('/')
    if ($separator -lt 1) { continue }
    $id = $key.Substring(0, $separator)
    $version = $key.Substring($separator + 1)
    $inventory.Add("$id $version")

    $packageDir = Join-Path (Join-Path $nugetRoot $id.ToLowerInvariant()) $version.ToLowerInvariant()
    if (-not (Test-Path -LiteralPath $packageDir -PathType Container)) {
        throw "Restored NuGet package directory was not found: $packageDir"
    }

    $noticeFiles = Get-ChildItem -LiteralPath $packageDir -Recurse -File | Where-Object {
        $_.Name -match '(?i)^(license|notice|third[-_. ]?party[-_. ]?notices?)(\.|$)'
    }
    foreach ($notice in $noticeFiles) {
        $safeId = $id -replace '[^A-Za-z0-9_]', '_'
        $safeVersion = $version -replace '[^A-Za-z0-9_]', '_'
        $relativeNotice = $notice.FullName.Substring($packageDir.Length).TrimStart([char[]]"\/")
        $safeNotice = $relativeNotice -replace '[^A-Za-z0-9_]', '_'
        $targetName = "${safeId}_${safeVersion}_${safeNotice}.txt"
        Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $legalRoot $targetName) -Force
        if ($id -eq "Microsoft.WindowsAppSDK" -and $notice.Name -match '(?i)^license\.') {
            Copy-Item -LiteralPath $notice.FullName `
                -Destination (Join-Path $legalRoot "Microsoft-WindowsAppSDK-LICENSE.txt") -Force
        }
    }
}

$dotnetExecutable = (Get-Command dotnet -ErrorAction Stop).Source
$dotnetRoot = Split-Path -Parent $dotnetExecutable
foreach ($name in @("LICENSE.txt", "ThirdPartyNotices.txt")) {
    $source = Join-Path $dotnetRoot $name
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw ".NET redistribution notice was not found: $source"
    }
    $safeName = $name -replace '[^A-Za-z0-9_]', '_'
    Copy-Item -LiteralPath $source -Destination (Join-Path $legalRoot "dotnet_$safeName.txt") -Force
}

$inventory.Add("")
$inventory.Add("Generated from: $AssetsFile")
$inventory.Add(".NET SDK: $(& dotnet --version)")
$inventory | Set-Content -LiteralPath (Join-Path $legalRoot "PACKAGES.txt") -Encoding utf8

Write-Host "Collected legal notices for $($packageKeys.Count) NuGet packages in $legalRoot"
