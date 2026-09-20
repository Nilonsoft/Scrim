<#
.SYNOPSIS
    Builds the Scrim Windows Installer (.msi) package using WiX Toolset v5.
.DESCRIPTION
    Publishes a self-contained win-x64 build of Scrim and compiles it into an MSI installer.
    Supports in-place auto-upgrade for newer versions.
.PARAMETER Version
    The version of the package to generate (defaults to 1.0.0).
.PARAMETER Configuration
    The build configuration (defaults to Release).
.EXAMPLE
    .\build-installer.ps1
    .\build-installer.ps1 -Version 1.0.1
#>

param(
    [string]$Version = "1.0.1",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host "       Building Scrim Windows Installer v$Version    " -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan

$scriptRoot = $PSScriptRoot
$projectFile = Join-Path $scriptRoot "src\Scrim.csproj"
$publishDir = Join-Path $scriptRoot "publish_build"
$wxsFile = Join-Path $scriptRoot "installer\Package.wxs"
$binDir = Join-Path $scriptRoot "bin"
$outputMsi = Join-Path $binDir "ScrimSetup-v$Version.msi"

if (-not (Test-Path $binDir)) {
    New-Item -ItemType Directory -Path $binDir -Force | Out-Null
}

Write-Host "`n[1/3] Checking WiX Toolset v5..." -ForegroundColor Yellow
$wixCmd = Get-Command "wix.exe" -ErrorAction SilentlyContinue
if (-not $wixCmd) {
    Write-Error "wix.exe not found on PATH. Please install WiX Toolset v5 using: dotnet tool install --global wix"
}
Write-Host "Found WiX: $($wixCmd.Source)" -ForegroundColor Green

Write-Host "`n[2/3] Publishing self-contained win-x64 application..." -ForegroundColor Yellow
& dotnet publish $projectFile -c $Configuration -r $Runtime --self-contained true -o $publishDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
}
Write-Host "Publish completed successfully." -ForegroundColor Green

# Clean up any leftover developer documentation in publish directory
$oldDocs = Join-Path $publishDir "docs"
if (Test-Path $oldDocs) {
    Remove-Item -Path $oldDocs -Recurse -Force
}
$oldReadme = Join-Path $publishDir "README.md"
if (Test-Path $oldReadme) {
    Remove-Item -Path $oldReadme -Force
}

# Package the end-user guide and plugin development guide
$userGuideSource = Join-Path $scriptRoot "docs\user-guide.md"
$userGuideDest = Join-Path $publishDir "UserGuide.md"
if (Test-Path $userGuideSource) {
    Copy-Item -Path $userGuideSource -Destination $userGuideDest -Force
    Write-Host "Packaged clean UserGuide.md for installer." -ForegroundColor Gray
}

$pluginGuideSource = Join-Path $scriptRoot "docs\plugin-development.md"
$pluginGuideDest = Join-Path $publishDir "PluginGuide.md"
if (Test-Path $pluginGuideSource) {
    Copy-Item -Path $pluginGuideSource -Destination $pluginGuideDest -Force
    Write-Host "Packaged PluginGuide.md for installer." -ForegroundColor Gray
}

# Ensure VoiceEffects and Plugins directories exist and are packaged into MSI
$voiceEffectsDir = Join-Path $publishDir "VoiceEffects"
if (-not (Test-Path $voiceEffectsDir)) {
    New-Item -ItemType Directory -Path $voiceEffectsDir -Force | Out-Null
}
$veReadme = Join-Path $voiceEffectsDir "readme.txt"
if (-not (Test-Path $veReadme)) {
    Set-Content -Path $veReadme -Value "Custom voice effects (.dll, .json, .lua) can be placed here or in ~/.scrim/voice_effects/."
}

$pluginsDir = Join-Path $publishDir "Plugins"
if (-not (Test-Path $pluginsDir)) {
    New-Item -ItemType Directory -Path $pluginsDir -Force | Out-Null
}
$plReadme = Join-Path $pluginsDir "readme.txt"
if (-not (Test-Path $plReadme)) {
    Set-Content -Path $plReadme -Value "Custom Scrim plugins (.dll) can be placed here or in ~/.scrim/plugins/."
}

# Build and package all built-in plugins from plugins/ directory
$pluginsSourceDir = Join-Path $scriptRoot "plugins"
if (Test-Path $pluginsSourceDir) {
    $pluginProjects = Get-ChildItem -Path $pluginsSourceDir -Filter "*.csproj" -Recurse
    foreach ($pluginProj in $pluginProjects) {
        $pName = [System.IO.Path]::GetFileNameWithoutExtension($pluginProj.Name)
        Write-Host "Compiling plugin: $pName..." -ForegroundColor Gray
        $pluginOutDir = Join-Path $pluginProj.DirectoryName "bin\$Configuration"
        & dotnet build $pluginProj.FullName -c $Configuration -r $Runtime --no-self-contained -o $pluginOutDir
        if ($LASTEXITCODE -eq 0) {
            $pluginDll = Join-Path $pluginOutDir "$pName.dll"
            if (Test-Path $pluginDll) {
                Copy-Item -Path $pluginDll -Destination (Join-Path $pluginsDir "$pName.dll") -Force
                Write-Host "Packaged $pName.dll into Plugins/." -ForegroundColor Gray
            }
            # Copy any json config files alongside the plugin
            Get-ChildItem -Path $pluginProj.DirectoryName -Filter "*.json" | ForEach-Object {
                Copy-Item -Path $_.FullName -Destination (Join-Path $pluginsDir $_.Name) -Force
                Write-Host "Packaged $($_.Name) into Plugins/." -ForegroundColor Gray
            }
        }
    }
}
# Remove development staticwebassets manifests and debug symbol files that contain local source paths
Get-ChildItem -Path $publishDir -Filter "*staticwebassets*.json" -Recurse | Remove-Item -Force
Get-ChildItem -Path $publishDir -Filter "*.pdb" -Recurse | Remove-Item -Force
Write-Host "Sanitized publish directory (removed dev staticwebassets and debug PDBs)." -ForegroundColor Gray

Write-Host "`n[3/3] Compiling WiX MSI Installer: $outputMsi..." -ForegroundColor Yellow
& wix build $wxsFile -arch x64 -d "Version=$Version" -d "PublishDir=$publishDir" -o $outputMsi
if ($LASTEXITCODE -ne 0) {
    Write-Error "WiX build failed with exit code $LASTEXITCODE"
}

$msiItem = Get-Item $outputMsi
$sizeMb = [Math]::Round($msiItem.Length / 1MB, 2)

Write-Host "`n====================================================" -ForegroundColor Green
Write-Host " Installer Built Successfully!" -ForegroundColor Green
Write-Host " File: $($msiItem.FullName)" -ForegroundColor White
Write-Host " Size: $sizeMb MB" -ForegroundColor White
Write-Host " Features: Start Menu & Desktop Shortcuts, In-Place Auto-Upgrade" -ForegroundColor Gray
Write-Host "====================================================" -ForegroundColor Green
