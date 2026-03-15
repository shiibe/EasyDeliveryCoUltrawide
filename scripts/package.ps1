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
$zipRoot = Join-Path $repoRoot "dist"
$manifestPath = Join-Path $repoRoot "manifest.json"
$changelogPath = Join-Path $repoRoot "CHANGELOG.md"
$readmePath = Join-Path $repoRoot "README.md"
$pluginCsPath = Join-Path $repoRoot "EasyDeliveryCoUltrawide\Plugin.cs"
$assemblyInfoPath = Join-Path $repoRoot "EasyDeliveryCoUltrawide\Properties\AssemblyInfo.cs"
$iconPath = Join-Path $repoRoot "assets\icon.png"

if (-not (Test-Path $distRoot))
{
    New-Item -ItemType Directory -Force -Path $distRoot | Out-Null
}

Write-Host "Building $Project ($Configuration)..."
dotnet build (Join-Path $repoRoot $Project) -c $Configuration

Write-Host "Updating manifest version..."
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$manifest.version_number = $Version
$manifest | ConvertTo-Json -Depth 10 | Set-Content -Path $manifestPath

Write-Host "Updating plugin version constants..."
if (Test-Path $pluginCsPath)
{
    $pluginCs = Get-Content $pluginCsPath -Raw
    $pluginCsUpdated = $pluginCs -replace 'public\s+const\s+string\s+PluginVersion\s*=\s*"[^"]*"\s*;', "public const string PluginVersion = `"$Version`";"
    if ($pluginCsUpdated -ne $pluginCs)
    {
        Set-Content -Path $pluginCsPath -Value $pluginCsUpdated
    }
    else
    {
        Write-Warning "PluginVersion constant not found in $pluginCsPath"
    }
}
else
{
    Write-Warning "Missing file: $pluginCsPath"
}

Write-Host "Updating assembly version attributes..."
if (Test-Path $assemblyInfoPath)
{
    $assemblyInfo = Get-Content $assemblyInfoPath -Raw
    $assemblyVersion = "$Version.0"
    $assemblyInfoUpdated = $assemblyInfo
    $assemblyInfoUpdated = $assemblyInfoUpdated -replace 'AssemblyVersion\("[^"]*"\)', "AssemblyVersion(`"$assemblyVersion`")"
    $assemblyInfoUpdated = $assemblyInfoUpdated -replace 'AssemblyFileVersion\("[^"]*"\)', "AssemblyFileVersion(`"$assemblyVersion`")"
    if ($assemblyInfoUpdated -ne $assemblyInfo)
    {
        Set-Content -Path $assemblyInfoPath -Value $assemblyInfoUpdated
    }
    else
    {
        Write-Warning "AssemblyVersion attributes not found in $assemblyInfoPath"
    }
}
else
{
    Write-Warning "Missing file: $assemblyInfoPath"
}

Write-Host "Updating changelog version header..."
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
if (-not (Test-Path $iconPath))
{
    throw "icon.png missing at $iconPath"
}

Write-Host "Syncing Thunderstore metadata..."
Copy-Item $manifestPath -Destination (Join-Path $distRoot "manifest.json") -Force
Copy-Item $changelogPath -Destination (Join-Path $distRoot "CHANGELOG.md") -Force
Copy-Item $readmePath -Destination (Join-Path $distRoot "README.md") -Force
Copy-Item $iconPath -Destination (Join-Path $distRoot "icon.png") -Force

Write-Host "Creating zip package..."
$zipName = "${pluginName}_$Version.zip"
$zipPath = Join-Path $zipRoot $zipName
if (Test-Path $zipRoot)
{
    Remove-Item (Join-Path $zipRoot "*") -Recurse -Force
}
else
{
    New-Item -ItemType Directory -Force -Path $zipRoot | Out-Null
}

Compress-Archive -Path (Join-Path $distRoot "*") -DestinationPath $zipPath

Write-Host "Package created: $zipPath"
