# DICOMizer Build Script
# PowerShell script to build and package the application

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$Clean,
    [switch]$Restore,
    [switch]$Build,
    [switch]$Publish,
    [switch]$DownloadFFmpeg,
    [switch]$CreateRelease,
    [switch]$All
)

$ErrorActionPreference = "Stop"

$ProjectName = "DICOMizer"
$ProjectFile = "$ProjectName.csproj"
$Version = "1.0.0"
$FFmpegUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip"

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

# Download FFmpeg
if ($DownloadFFmpeg -or $CreateRelease) {
    Write-Host "[FFmpeg] Downloading FFmpeg..." -ForegroundColor Yellow

    $ffmpegZip = "ffmpeg.zip"
    $ffmpegExtract = "ffmpeg-temp"

    # Download
    Invoke-WebRequest -Uri $FFmpegUrl -OutFile $ffmpegZip
    Write-Host "Downloaded FFmpeg" -ForegroundColor Green

    # Extract
    if (Test-Path $ffmpegExtract) { Remove-Item -Recurse -Force $ffmpegExtract }
    Expand-Archive -Path $ffmpegZip -DestinationPath $ffmpegExtract

    # Find and copy binaries
    $ffmpegBin = Get-ChildItem -Path $ffmpegExtract -Recurse -Filter "ffmpeg.exe" | Select-Object -First 1
    $ffprobeBin = Get-ChildItem -Path $ffmpegExtract -Recurse -Filter "ffprobe.exe" | Select-Object -First 1

    if (-not (Test-Path "Resources\FFmpeg")) {
        New-Item -ItemType Directory -Path "Resources\FFmpeg" -Force | Out-Null
    }

    Copy-Item $ffmpegBin.FullName -Destination "Resources\FFmpeg\ffmpeg.exe" -Force
    Copy-Item $ffprobeBin.FullName -Destination "Resources\FFmpeg\ffprobe.exe" -Force

    # Cleanup
    Remove-Item $ffmpegZip -Force
    Remove-Item -Recurse -Force $ffmpegExtract

    Write-Host "FFmpeg installed to Resources\FFmpeg\" -ForegroundColor Green
    Write-Host ""
}

# Create Release Package
if ($CreateRelease) {
    Write-Host "[Release] Creating release package..." -ForegroundColor Yellow

    $ReleaseDir = "release"
    $ReleaseName = "$ProjectName-v$Version-win64"
    $ReleaseFolder = "$ReleaseDir\$ReleaseName"

    # Clean release folder
    if (Test-Path $ReleaseDir) { Remove-Item -Recurse -Force $ReleaseDir }
    New-Item -ItemType Directory -Path $ReleaseFolder -Force | Out-Null

    # Publish self-contained single file
    Write-Host "Building self-contained executable..." -ForegroundColor Yellow
    dotnet publish $ProjectFile `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o "$ReleaseFolder"

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Publish failed" -ForegroundColor Red
        exit 1
    }

    # Copy FFmpeg
    if (Test-Path "Resources\FFmpeg\ffmpeg.exe") {
        New-Item -ItemType Directory -Path "$ReleaseFolder\FFmpeg" -Force | Out-Null
        Copy-Item "Resources\FFmpeg\ffmpeg.exe" -Destination "$ReleaseFolder\FFmpeg\" -Force
        Copy-Item "Resources\FFmpeg\ffprobe.exe" -Destination "$ReleaseFolder\FFmpeg\" -Force
    }

    # Create zip
    $ZipPath = "$ReleaseDir\$ReleaseName.zip"
    Compress-Archive -Path "$ReleaseFolder\*" -DestinationPath $ZipPath -Force

    # Cleanup folder, keep zip
    Remove-Item -Recurse -Force $ReleaseFolder

    $ZipSize = [math]::Round((Get-Item $ZipPath).Length / 1MB, 1)
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  Release package created!" -ForegroundColor Green
    Write-Host "  $ZipPath ($ZipSize MB)" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "To upload to GitHub:" -ForegroundColor Yellow
    Write-Host "  1. Go to https://github.com/Kartikey-18/DICOMizer/releases" -ForegroundColor White
    Write-Host "  2. Click 'Create a new release'" -ForegroundColor White
    Write-Host "  3. Tag: v$Version" -ForegroundColor White
    Write-Host "  4. Upload: $ZipPath" -ForegroundColor White
    Write-Host ""
    exit 0
}

# Check for FFmpeg
if (-not (Test-Path "Resources\FFmpeg\ffmpeg.exe")) {
    Write-Host "WARNING: FFmpeg not found!" -ForegroundColor Yellow
    Write-Host "  Run: .\build.ps1 -DownloadFFmpeg" -ForegroundColor Yellow
    Write-Host "  Or manually place ffmpeg.exe and ffprobe.exe in Resources\FFmpeg\" -ForegroundColor Yellow
    Write-Host ""
}
