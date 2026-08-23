[CmdletBinding()]
param(
    [string]$ManifestPath = "Package.appxmanifest",
    [switch]$AllowDevelopmentIdentity
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestFullPath = if ([System.IO.Path]::IsPathRooted($ManifestPath)) {
    [System.IO.Path]::GetFullPath($ManifestPath)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ManifestPath))
}

[xml]$manifest = Get-Content -LiteralPath $manifestFullPath -Raw
$ns = [System.Xml.XmlNamespaceManager]::new($manifest.NameTable)
$ns.AddNamespace("f", "http://schemas.microsoft.com/appx/manifest/foundation/windows10")

$identity = $manifest.SelectSingleNode("/f:Package/f:Identity", $ns)
$properties = $manifest.SelectSingleNode("/f:Package/f:Properties", $ns)
if (-not $identity -or -not $properties) { throw "Identity or Properties is missing." }

$name = $identity.GetAttribute("Name")
$publisher = $identity.GetAttribute("Publisher")
$version = $identity.GetAttribute("Version")
$publisherDisplayName = $properties.PublisherDisplayName

if (-not $AllowDevelopmentIdentity -and
    ($name -eq "XTimelineViewerKotsume.Development" -or $publisher -eq "CN=Kotsume Development")) {
    throw "Development Identity is still present. Replace it with the exact Partner Center Product identity before submission."
}

if ($name -eq "4275.XTimelineViewer" -or $publisher -match "B73FDB0C" -or $publisherDisplayName -eq "だるやなぎ") {
    throw "The upstream Store identity must not be reused."
}

if ($version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "Package version must contain four numeric parts: $version"
}

[xml]$project = Get-Content -LiteralPath (Join-Path $repoRoot "XTimelineViewer.csproj") -Raw
$appVersion = [string]($project.Project.PropertyGroup.Version | Select-Object -First 1)
$expectedPackageVersion = "$appVersion.0"
if ($version -ne $expectedPackageVersion) {
    throw "Package version $version does not match application version $appVersion (expected $expectedPackageVersion)."
}

Write-Host "Store manifest readiness checks passed: Name=$name Publisher=$publisher Version=$version"
