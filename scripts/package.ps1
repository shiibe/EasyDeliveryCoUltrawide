param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$Configuration = "Release",
    [string]$Project = "EasyDeliveryCoUltrawide/EasyDeliveryCoUltrawide.csproj"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$pluginName = "EasyDeliveryCoUltrawide"
$distRoot = Join-Path $repoRoot "Thunderstore"
$distPlugins = Join-Path $distRoot "BepInEx\plugins\$pluginName"

if (-not (Test-Path $distRoot))
{
    throw "Thunderstore folder not found at $distRoot"
}

Write-Host "Building $Project ($Configuration)..."
dotnet build (Join-Path $repoRoot $Project) -c $Configuration

Write-Host "Updating manifest version..."
$manifestPath = Join-Path $distRoot "manifest.json"
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$manifest.version_number = $Version
$manifest | ConvertTo-Json -Depth 10 | Set-Content -Path $manifestPath

Write-Host "Updating changelog version header..."
$changelogPath = Join-Path $distRoot "CHANGELOG.md"
$changelog = Get-Content $changelogPath -Raw
if ($changelog -notmatch "(?m)^##\s+$Version\s*$")
{
    $changelog = "## $Version`r`n- Packaged build`r`n`r`n" + $changelog.Trim()
    Set-Content -Path $changelogPath -Value $changelog
}

Write-Host "Copying plugin binaries..."
$outputDir = Join-Path (Join-Path $repoRoot "EasyDeliveryCoUltrawide") ("bin\" + $Configuration + "\net472")
$dllPath = Join-Path $outputDir ($pluginName + ".dll")
$pdbPath = Join-Path $outputDir ($pluginName + ".pdb")

if (-not (Test-Path $dllPath))
{
    throw "Build output not found at $dllPath"
}

New-Item -ItemType Directory -Force -Path $distPlugins | Out-Null
Copy-Item $dllPath -Destination $distPlugins -Force
if (Test-Path $pdbPath)
{
    Copy-Item $pdbPath -Destination $distPlugins -Force
}

Write-Host "Ensuring icon exists..."
$iconPath = Join-Path $distRoot "icon.png"
if (-not (Test-Path $iconPath))
{
    throw "icon.png missing at $iconPath"
}

Write-Host "Creating zip package..."
$zipName = "${pluginName}_$Version.zip"
$zipPath = Join-Path $repoRoot $zipName
if (Test-Path $zipPath)
{
    Remove-Item $zipPath -Force
}

Compress-Archive -Path (Join-Path $distRoot "*") -DestinationPath $zipPath

Write-Host "Package created: $zipPath"
