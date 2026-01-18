# DICOMizer Build Script
# PowerShell script to build and package the application

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$Clean,
    [switch]$Restore,
    [switch]$Build,
    [switch]$Publish,
    [switch]$All
)

$ErrorActionPreference = "Stop"

$ProjectName = "DICOMizer"
$ProjectFile = "$ProjectName.csproj"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  DICOMizer Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Set default to All if no switches specified
if (-not ($Clean -or $Restore -or $Build -or $Publish)) {
    $All = $true
}

# Clean
if ($Clean -or $All) {
    Write-Host "[1/4] Cleaning previous builds..." -ForegroundColor Yellow
    if (Test-Path "bin") { Remove-Item -Recurse -Force "bin" }
    if (Test-Path "obj") { Remove-Item -Recurse -Force "obj" }
    Write-Host "✓ Clean completed" -ForegroundColor Green
    Write-Host ""
}

# Restore
if ($Restore -or $All) {
    Write-Host "[2/4] Restoring NuGet packages..." -ForegroundColor Yellow
    dotnet restore $ProjectFile
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Restore failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "✓ Restore completed" -ForegroundColor Green
    Write-Host ""
}

# Build
if ($Build -or $All) {
    Write-Host "[3/4] Building $Configuration configuration..." -ForegroundColor Yellow
    dotnet build $ProjectFile -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Build failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "✓ Build completed" -ForegroundColor Green
    Write-Host ""
}

# Publish
if ($Publish -or $All) {
    Write-Host "[4/4] Publishing application..." -ForegroundColor Yellow

    $PublishDir = "publish\$Configuration"

    # Publish as self-contained
    dotnet publish $ProjectFile `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=false `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $PublishDir

    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Publish failed" -ForegroundColor Red
        exit 1
    }

    Write-Host "✓ Publish completed" -ForegroundColor Green
    Write-Host ""
    Write-Host "Published to: $PublishDir" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check for FFmpeg
if (-not (Test-Path "Resources\FFmpeg\ffmpeg.exe")) {
    Write-Host "⚠ WARNING: FFmpeg not found!" -ForegroundColor Yellow
    Write-Host "  Please download FFmpeg and place ffmpeg.exe and ffprobe.exe" -ForegroundColor Yellow
    Write-Host "  in the Resources\FFmpeg\ directory" -ForegroundColor Yellow
    Write-Host ""
}
