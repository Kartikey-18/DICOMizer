# DICOMizer - Quick Start Guide

Get up and running with DICOMizer in 5 minutes!

## Prerequisites

Before you start, make sure you have:
- ✅ Windows 10 or later (64-bit)
- ✅ .NET 8.0 SDK installed
- ✅ Visual Studio 2022 (or VS Code with C# extension)

## Step 1: Get FFmpeg (2 minutes)

DICOMizer needs FFmpeg to process videos.

1. Download FFmpeg: https://ffmpeg.org/download.html
   - Click **Windows** → **Windows builds from gyan.dev**
   - Download **ffmpeg-release-essentials.zip**

2. Extract the ZIP file

3. Copy these files to `DICOMizer/Resources/FFmpeg/`:
   - `ffmpeg.exe`
   - `ffprobe.exe`

   (Find them in the extracted folder's `bin` subfolder)

## Step 2: Build the Project (1 minute)

### Option A: Using Visual Studio
```
1. Open DICOMizer.sln
2. Press F5 (Build & Run)
```

### Option B: Using Command Line
```bash
cd DICOMizer
dotnet restore
dotnet build
dotnet run
```

### Option C: Using Build Script
```powershell
.\build.ps1 -All
```

## Step 3: First Run (2 minutes)

When you first run DICOMizer:

1. **Main Window** will appear
2. Click **Browse** to select a video file
3. Enter **Patient Information**:
   - Patient ID (required)
   - Patient Name (required)
   - Other fields are optional

4. Choose output options:
   - ✅ **Save to file** (saves to Downloads/DICOM folder)
   - ⬜ **Send to PACS** (configure PACS settings first)

5. Click **Convert to DICOM**

That's it! Your DICOM file will be created.

## Optional: Configure PACS (if needed)

If you want to send files directly to a PACS server:

1. Click **⚙ Settings** button
2. Enter PACS details:
   ```
   Host: 192.168.1.100 (your PACS IP)
   Port: 104
   AE Title: DICOMIZER
   Called AE Title: EUNITY (your PACS AE Title)
   ```
3. Click **Test Connection**
4. Click **Save Settings**

Now you can check "Send to PACS" when converting videos.

## Common Workflows

### Basic Conversion
```
Browse → Select Video → Enter Patient Info → Convert
```

### Trim Then Convert
```
Browse → Select Video → Trim Video → Set Start/End → Apply → Enter Patient Info → Convert
```

### Send to PACS
```
Settings → Configure PACS → Test Connection → Save
Browse → Select Video → Enter Patient Info → Check "Send to PACS" → Convert
```

## File Locations

| What | Where |
|------|-------|
| Output DICOM files | `%USERPROFILE%\Downloads\DICOM\` |
| Settings | `%APPDATA%\DICOMizer\settings.json` |
| Log files | `%APPDATA%\DICOMizer\Logs\` |
| Temp files | `%APPDATA%\DICOMizer\Temp\` |

## Supported Video Formats

- ✅ MP4 (.mp4)
- ✅ AVI (.avi)
- ✅ MOV (.mov)
- ✅ MKV (.mkv)
- ✅ WMV (.wmv)
- ✅ FLV (.flv)
- ✅ M4V (.m4v)

**Max file size**: 5 GB

## Features

| Feature | Description |
|---------|-------------|
| 🎬 **Video Preview** | Watch video before conversion |
| ✂️ **Trimming** | Cut start/end of video |
| 🎯 **Frame Navigation** | Navigate frame-by-frame |
| 📊 **Progress Tracking** | Real-time conversion progress |
| ⚡ **Hardware Acceleration** | GPU-accelerated encoding |
| 🔒 **Validation** | Input validation & security checks |
| 📝 **Logging** | Detailed logs for troubleshooting |
| 💾 **Settings Persistence** | Remember PACS configuration |

## Troubleshooting

### "FFmpeg not found"
→ Copy `ffmpeg.exe` and `ffprobe.exe` to `Resources/FFmpeg/` folder

### "Invalid video format"
→ Use supported formats (MP4, AVI, MOV, etc.)

### "File too large"
→ Video exceeds 5 GB limit - try compressing first

### "PACS connection failed"
→ Check PACS IP, port, and AE titles in Settings

### "Patient validation error"
→ Patient ID and Patient Name are required fields

## Tips & Best Practices

1. **Video Quality**: Use 720p or 1080p for best results
2. **File Size**: Smaller files process faster
3. **Frame Rate**: 25 FPS is standard for medical videos
4. **Trimming**: Trim unnecessary parts before conversion to save time
5. **PACS Testing**: Always test PACS connection before bulk operations
6. **Backup**: Output files are saved to Downloads/DICOM - back up regularly

## Next Steps

- Read [SETUP.md](SETUP.md) for detailed installation
- Check [PROJECT_STATUS.md](PROJECT_STATUS.md) for implementation details
- Review [README.md](README.md) for technical information

## Need Help?

1. Check the log files in `%APPDATA%\DICOMizer\Logs\`
2. Review error messages in the application
3. Consult technical documentation
4. Contact your system administrator

---

**Enjoy using DICOMizer!** 🎉
