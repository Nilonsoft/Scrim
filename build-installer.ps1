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
    [string]$Version = "1.0.0",
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
