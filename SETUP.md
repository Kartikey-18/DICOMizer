# DICOMizer Setup Guide

## Prerequisites

### Required Software
- **Windows 10 or later** (64-bit)
- **.NET 8.0 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Visual Studio 2022** (Community Edition or higher) with:
  - .NET desktop development workload
  - WPF components

### Required Dependencies
- **FFmpeg** - Must be placed in `Resources/FFmpeg/` directory
  - Download from: https://ffmpeg.org/download.html
  - Required files:
    - `ffmpeg.exe`
    - `ffprobe.exe`

## Installation Steps

### 1. Clone or Download the Project

```bash
git clone <repository-url>
cd DICOMizer
```

### 2. Install FFmpeg

1. Download FFmpeg for Windows (static build recommended)
2. Extract the archive
3. Copy `ffmpeg.exe` and `ffprobe.exe` to:
   ```
   DICOMizer/Resources/FFmpeg/
   ```

### 3. Restore NuGet Packages

Open the solution in Visual Studio or run:

```bash
dotnet restore
```

The following packages will be installed:
- fo-dicom v5.2.2
- fo-dicom.Codecs v5.1.6.4
- fo-dicom.Network v5.2.2

### 4. Build the Project

In Visual Studio:
- Open `DICOMizer.sln`
- Select **Build > Build Solution** (or press F6)

Or via command line:

```bash
dotnet build
```

### 5. Run the Application

In Visual Studio:
- Press F5 or click the Start button

Or via command line:

```bash
dotnet run
```

## Configuration

### PACS Server Setup

1. Launch DICOMizer
2. Click the **Settings** button (⚙)
3. Enter your PACS server details:
   - **Host/IP Address**: PACS server hostname or IP
   - **Port**: DICOM port (default: 104)
   - **AE Title**: Application Entity title for DICOMizer
   - **Called AE Title**: PACS server's AE title
   - **Timeout**: Connection timeout in seconds
4. Click **Test Connection** to verify
5. Click **Save Settings**

### First-Time Configuration

On first run, the application will:
- Create configuration directory in `%APPDATA%\DICOMizer`
- Create default output directory in `%USERPROFILE%\Downloads\DICOM`
- Generate default settings file
- Create logs directory

## Directory Structure

```
DICOMizer/
├── Models/              # Data models
├── Services/            # Business logic
├── Utilities/           # Helper classes
├── Views/              # WPF windows
├── Resources/          # Assets
│   └── FFmpeg/         # FFmpeg binaries
├── App.xaml            # Application entry
├── DICOMizer.csproj    # Project file
└── README.md           # Documentation
```

## Application Data Locations

- **Settings**: `%APPDATA%\DICOMizer\settings.json`
- **Logs**: `%APPDATA%\DICOMizer\Logs\`
- **Temp Files**: `%APPDATA%\DICOMizer\Temp\`
- **Output Files**: `%USERPROFILE%\Downloads\DICOM\`

## Troubleshooting

### FFmpeg Not Found

**Error**: "FFmpeg executable not found"

**Solution**:
1. Verify `ffmpeg.exe` and `ffprobe.exe` are in `Resources/FFmpeg/`
2. Ensure files are set to "Copy to Output Directory" in Visual Studio
3. Rebuild the project

### DICOM Packages Not Loading

**Error**: "Could not load fo-dicom assemblies"

**Solution**:
1. Delete `bin/` and `obj/` folders
2. Run `dotnet restore`
3. Rebuild the project

### PACS Connection Failed

**Error**: "Failed to connect to PACS server"

**Possible Causes**:
- Incorrect host/IP address
- Wrong port number
- Firewall blocking connection
- PACS server not running
- Incorrect AE titles

**Solution**:
1. Verify PACS server is running
2. Check network connectivity: `ping <pacs-host>`
3. Verify PACS port is open
4. Confirm AE titles match PACS configuration
5. Check firewall settings

### Video Format Not Supported

**Error**: "Invalid video file format"

**Supported Formats**:
- MP4 (.mp4)
- AVI (.avi)
- MOV (.mov)
- MKV (.mkv)
- WMV (.wmv)
- FLV (.flv)
- M4V (.m4v)

**Solution**: Convert your video to a supported format using a video converter

## Building for Distribution

### Debug Build

```bash
dotnet build -c Debug
```

### Release Build

```bash
dotnet build -c Release
```

### Publish as Self-Contained

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

This creates a standalone executable that includes the .NET runtime.

## System Requirements

### Minimum
- Windows 10 (64-bit)
- 4 GB RAM
- 500 MB disk space
- .NET 8.0 Runtime

### Recommended
- Windows 11 (64-bit)
- 8 GB RAM
- 2 GB disk space (for processing large videos)
- Hardware-accelerated GPU (for faster video encoding)

## License

Proprietary - All Rights Reserved

## Support

For issues and questions, please contact your system administrator or refer to the technical documentation.
