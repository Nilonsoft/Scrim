param(
    [switch]$Clean,
    [switch]$Build,
    [switch]$Test,
    [switch]$Publish
)

$ProjectPath = ".\src\Scrim.csproj"
$OutputPublishDir = ".\publish"

if ($Clean) {
    Write-Host "Cleaning solution..." -ForegroundColor Cyan
    dotnet clean ".\src\Scrim.sln"
    if (Test-Path $OutputPublishDir) {
        Remove-Item -Recurse -Force $OutputPublishDir
    }
}

if ($Build) {
    Write-Host "Building solution..." -ForegroundColor Cyan
    dotnet build ".\src\Scrim.sln" -c Release
}

if ($Test) {
    Write-Host "Running tests..." -ForegroundColor Cyan
    dotnet test ".\src\Scrim.sln" -c Release
}

if ($Publish) {
    Write-Host "Publishing self-contained executable..." -ForegroundColor Cyan
    # Publish as self-contained, single file for Windows
    dotnet publish $ProjectPath -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $OutputPublishDir
    Write-Host "Publish complete. Output located in $OutputPublishDir" -ForegroundColor Green
}

if (-not $Clean -and -not $Build -and -not $Test -and -not $Publish) {
    Write-Host "Usage: .\scrim.ps1 [-Clean] [-Build] [-Test] [-Publish]" -ForegroundColor Yellow
}
