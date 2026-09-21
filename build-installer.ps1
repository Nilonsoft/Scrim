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
    .\build-installer.ps1 -Version 1.0.4
#>

param(
    [string]$Version = "1.0.4",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$PatchNotesPath = ""
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

# Helper function to convert markdown documentation into standalone styled HTML for end users
function Convert-MarkdownToHtml {
    param(
        [string]$MarkdownPath,
        [string]$OutputPath,
        [string]$Title = "Scrim Documentation"
    )

    if (-not (Test-Path $MarkdownPath)) {
        return
    }

    $rawContent = Get-Content -Path $MarkdownPath -Raw -Encoding utf8

    # Convert GitHub alert blockquotes to styled alert divs
    $alertRegex = '(?ms)^>\s*\[!(NOTE|TIP|IMPORTANT|WARNING|CAUTION)\]\s*\r?\n((?:^>.*?(?:\r?\n|$))+)'
    $processed = [System.Text.RegularExpressions.Regex]::Replace($rawContent, $alertRegex, {
        param($match)
        $type = $match.Groups[1].Value.ToLower()
        $body = $match.Groups[2].Value -replace '(?m)^>\s?', ''
        $icon = switch ($type) {
            'note' { 'ℹ️' }
            'tip' { '💡' }
            'important' { '⭐' }
            'warning' { '⚠️' }
            'caution' { '🛑' }
            default { 'ℹ️' }
        }
        return "`n<div class=`"alert alert-$type`"><div class=`"alert-title`">$icon $($match.Groups[1].Value)</div>`n$body`n</div>`n"
    })

    $htmlBody = ($processed | ConvertFrom-Markdown).Html

    $htmlDocument = @"
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>$Title - Scrim</title>
    <style>
        :root {
            --bg-color: #0d1117;
            --surface-color: #161b22;
            --border-color: #30363d;
            --text-color: #e6edf3;
            --text-muted: #8b949e;
            --accent-color: #58a6ff;
            --code-bg: #1c2128;
        }
        * { box-sizing: border-box; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", "Noto Sans", Helvetica, Arial, sans-serif;
            background-color: var(--bg-color);
            color: var(--text-color);
            line-height: 1.6;
            margin: 0;
            padding: 0;
        }
        .container {
            max-width: 920px;
            margin: 0 auto;
            padding: 40px 24px 80px 24px;
        }
        .header-bar {
            display: flex;
            align-items: center;
            justify-content: space-between;
            padding-bottom: 24px;
            margin-bottom: 32px;
            border-bottom: 1px solid var(--border-color);
        }
        .brand {
            display: flex;
            align-items: center;
            gap: 12px;
            font-weight: 700;
            font-size: 18px;
            color: #ffffff;
        }
        .brand-badge {
            background: linear-gradient(135deg, #8b5cf6, #3b82f6);
            color: #ffffff;
            font-size: 11px;
            padding: 3px 8px;
            border-radius: 6px;
            font-weight: 600;
        }
        h1, h2, h3, h4 {
            color: #ffffff;
            font-weight: 600;
            margin-top: 1.5em;
            margin-bottom: 0.5em;
            line-height: 1.3;
        }
        h1 {
            font-size: 28px;
            border-bottom: 1px solid var(--border-color);
            padding-bottom: 12px;
            margin-top: 0;
        }
        h2 {
            font-size: 20px;
            padding-bottom: 6px;
            border-bottom: 1px solid rgba(48, 54, 61, 0.5);
        }
        h3 { font-size: 16px; }
        p, li {
            font-size: 14px;
            color: var(--text-color);
        }
        a {
            color: var(--accent-color);
            text-decoration: none;
        }
        a:hover { text-decoration: underline; }
        pre {
            background-color: var(--code-bg);
            border: 1px solid var(--border-color);
            border-radius: 8px;
            padding: 16px;
            overflow-x: auto;
            font-size: 13px;
            line-height: 1.5;
        }
        code {
            font-family: ui-monospace, SFMono-Regular, "SF Mono", Menlo, Consolas, monospace;
            font-size: 85%;
            background-color: rgba(110, 118, 129, 0.2);
            padding: 0.2em 0.4em;
            border-radius: 4px;
        }
        pre code {
            background: transparent;
            padding: 0;
        }
        table {
            border-collapse: collapse;
            width: 100%;
            margin: 20px 0;
            font-size: 13px;
        }
        table th, table td {
            border: 1px solid var(--border-color);
            padding: 10px 14px;
            text-align: left;
        }
        table th {
            background-color: var(--surface-color);
            font-weight: 600;
            color: #ffffff;
        }
        table tr:nth-child(even) {
            background-color: rgba(22, 27, 34, 0.5);
        }
        hr {
            border: none;
            border-top: 1px solid var(--border-color);
            margin: 32px 0;
        }
        .alert {
            border-radius: 8px;
            padding: 14px 18px;
            margin: 20px 0;
            border-left: 4px solid;
            background: var(--surface-color);
            font-size: 13px;
        }
        .alert-title {
            font-weight: 700;
            margin-bottom: 6px;
            text-transform: uppercase;
            font-size: 11px;
            letter-spacing: 0.5px;
        }
        .alert-tip {
            border-color: #2ea043;
            background: rgba(46, 160, 67, 0.1);
        }
        .alert-tip .alert-title { color: #3fb950; }
        .alert-note {
            border-color: #1f6feb;
            background: rgba(31, 111, 235, 0.1);
        }
        .alert-note .alert-title { color: #58a6ff; }
        .alert-important {
            border-color: #8957e5;
            background: rgba(137, 87, 229, 0.1);
        }
        .alert-important .alert-title { color: #a371f7; }
        .alert-warning {
            border-color: #d29922;
            background: rgba(210, 153, 34, 0.1);
        }
        .alert-warning .alert-title { color: #e3b341; }
        .alert-caution {
            border-color: #f85149;
            background: rgba(248, 81, 73, 0.1);
        }
        .alert-caution .alert-title { color: #f85149; }
        footer {
            margin-top: 60px;
            padding-top: 20px;
            border-top: 1px solid var(--border-color);
            font-size: 12px;
            color: var(--text-muted);
            display: flex;
            justify-content: space-between;
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="header-bar">
            <div class="brand">
                <span>📻 Scrim</span>
                <span class="brand-badge">Documentation</span>
            </div>
            <div style="font-size: 12px; color: var(--text-muted);">
                Live Audio Streaming Console
            </div>
        </div>
        $htmlBody
        <footer>
            <div>Scrim Studio Documentation</div>
            <div>Built for Scrim v$Version</div>
        </footer>
    </div>
</body>
</html>
"@

    $dir = Split-Path -Path $OutputPath -Parent
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    [System.IO.File]::WriteAllText($OutputPath, $htmlDocument, [System.Text.Encoding]::UTF8)
}

# Clean up any leftover developer documentation and markdown files in publish directory
$oldDocs = Join-Path $publishDir "docs"
if (Test-Path $oldDocs) {
    Remove-Item -Path $oldDocs -Recurse -Force
}
$oldReadme = Join-Path $publishDir "README.md"
if (Test-Path $oldReadme) {
    Remove-Item -Path $oldReadme -Force
}
Get-ChildItem -Path $publishDir -Filter "*.md" -ErrorAction SilentlyContinue | Remove-Item -Force

# Convert and package rich HTML documentation for installer
$userGuideSource = Join-Path $scriptRoot "docs\user-guide.md"
$userGuideDest = Join-Path $publishDir "UserGuide.html"
Convert-MarkdownToHtml -MarkdownPath $userGuideSource -OutputPath $userGuideDest -Title "Scrim User Guide"
Write-Host "Packaged UserGuide.html for installer." -ForegroundColor Gray

$pluginGuideSource = Join-Path $scriptRoot "docs\plugin-development.md"
$pluginGuideDest = Join-Path $publishDir "PluginGuide.html"
Convert-MarkdownToHtml -MarkdownPath $pluginGuideSource -OutputPath $pluginGuideDest -Title "Scrim Plugin Development Guide"
Write-Host "Packaged PluginGuide.html for installer." -ForegroundColor Gray

$streamingGuideSource = Join-Path $scriptRoot "docs\easy-streaming-guide.md"
$streamingGuideDest = Join-Path $publishDir "EasyStreamingGuide.html"
Convert-MarkdownToHtml -MarkdownPath $streamingGuideSource -OutputPath $streamingGuideDest -Title "Scrim Easy Streaming Guide (Caddy & HTTPS)"
Write-Host "Packaged EasyStreamingGuide.html for installer." -ForegroundColor Gray

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
Set-Content -Path $plReadme -Value "Custom Scrim plugins (.dll assemblies or Python .py/folders) can be placed here or in ~/.scrim/plugins/."

# Copy scrim.py SDK into Plugins directory for Python plugin developers
$scrimPySource = Join-Path $scriptRoot "src\Plugins\scrim.py"
if (Test-Path $scrimPySource) {
    Copy-Item -Path $scrimPySource -Destination (Join-Path $pluginsDir "scrim.py") -Force
    Write-Host "Packaged scrim.py SDK into Plugins/." -ForegroundColor Gray
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

# Generate / copy release patch notes into the same folder as the installer
$patchNotesFileMd = Join-Path $binDir "PatchNotes-v$Version.md"
$patchNotesFileTxt = Join-Path $binDir "PatchNotes-v$Version.txt"

$sourcePatchNotes = $null
if (-not [string]::IsNullOrWhiteSpace($PatchNotesPath) -and (Test-Path $PatchNotesPath)) {
    $sourcePatchNotes = $PatchNotesPath
} else {
    $candidateFiles = @(
        (Join-Path $scriptRoot "docs\patch-notes-v$Version.md"),
        (Join-Path $scriptRoot "docs\patchnotes-v$Version.md"),
        (Join-Path $scriptRoot "docs\patch-notes.md"),
        (Join-Path $scriptRoot "PATCHNOTES.md")
    )
    foreach ($cand in $candidateFiles) {
        if (Test-Path $cand) {
            $sourcePatchNotes = $cand
            break
        }
    }
}

if ($sourcePatchNotes) {
    $patchNotesContent = Get-Content -Path $sourcePatchNotes -Raw -Encoding utf8
} else {
    $releaseDate = (Get-Date).ToString("yyyy-MM-dd")
    $patchNotesContent = @"
# Scrim Release v$Version Patch Notes
**Release Date:** $releaseDate
**Installer Package:** ScrimSetup-v$Version.msi

## What's New in v$Version

### 🚀 Client Update System & In-Place Auto-Update
- **Integrated NilonSoft Client Update API**: Real-time update checks against https://www.nilonsoft.com/api/updates/scrim.
- **Automatic Startup Checks**: Background asynchronous update queries 2 seconds after Scrim console launches.
- **Daily Background Scheduled Task**: Automatically registers a user-level Windows Scheduled Task (``Scrim Daily Update Check``) that queries updates daily even when Scrim is closed.
- **1-Click In-Place Auto-Update**: Dark-themed update dialog with release highlights, download progress, and auto-update flow:
  - Downloads the latest MSI package to temporary storage.
  - Automatically identifies and cleanly terminates running Scrim instances.
  - Runs silent/passive in-place upgrade without requiring UAC administrator elevation.
  - Automatically relaunches the updated Scrim application.

### 🎚️ Broadcast & Audio Engine
- Hardware-style microphone controls with push-to-talk, push-to-mute, and 25ms local headphone sidetone monitoring.
- Real-time DSP voice changers (Pitch & Formant shifting) with built-in presets and Lua/JSON/DLL extension support.
- 1-Click Virtual Audio Device driver installation with automatic loopback stream routing.
- Real-time multi-codec live transcoding supporting MP3, AAC, Opus, and lossless FLAC.
- Calibrated stereo dB VU meters with numeric peak readouts and auto-scaling attack/release dynamics.

### 🌐 Web Station & Interactive Player
- Dark glassmorphism web player with real-time metadata synchronized via Windows System Media Transport Controls (SMTC).
- Interactive anonymous live chat with host moderation controls.
- Listener song requests and dedications in real-time queue.
"@
}

[System.IO.File]::WriteAllText($patchNotesFileMd, $patchNotesContent, [System.Text.Encoding]::UTF8)

# Also generate plain-text version for quick console/notepad reading
$plainTextNotes = ($patchNotesContent -replace '#{1,6}\s*', '' -replace '\*\*', '' -replace '\[([^\]]+)\]\([^)]+\)', '$1').Trim()
[System.IO.File]::WriteAllText($patchNotesFileTxt, $plainTextNotes, [System.Text.Encoding]::UTF8)

Write-Host "`n====================================================" -ForegroundColor Green
Write-Host " Installer & Patch Notes Built Successfully!" -ForegroundColor Green
Write-Host " File:        $($msiItem.FullName)" -ForegroundColor White
Write-Host " Size:        $sizeMb MB" -ForegroundColor White
Write-Host " Patch Notes: $patchNotesFileMd" -ForegroundColor White
Write-Host " Features:    Start Menu & Desktop Shortcuts, In-Place Auto-Upgrade" -ForegroundColor Gray
Write-Host "====================================================" -ForegroundColor Green
