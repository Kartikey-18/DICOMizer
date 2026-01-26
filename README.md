# DICOMizer

A WPF application for converting video files to DICOM format compatible with eUnity PACS systems.

## Features

- Convert video files (MP4, AVI, MOV, MKV, WMV, FLV, M4V) to DICOM format
- Video trimming functionality
- Patient metadata entry with validation
- Direct PACS transmission via DICOM C-STORE
- eUnity-compatible video encoding
- Video preview and analysis

## Requirements

- .NET 8.0 Runtime
- Windows 10 or later
- FFmpeg (bundled in Resources/FFmpeg)

## Build Instructions

```bash
dotnet restore
dotnet build -c Release
dotnet run
```

## Project Structure

```
DICOMizer/
├── Models/          # Data models (PatientMetadata, VideoMetadata, etc.)
├── Services/        # Business logic (DicomConversionService, VideoProcessingService)
├── Utilities/       # Helper classes (PathHelper, UidGenerator, ProcessRunner)
├── Views/           # WPF UI windows
└── Resources/       # Assets and FFmpeg binaries
```

## Technical Specifications

### Video Encoding (eUnity Compatible)

Based on analysis of working eUnity DICOM video files, the following encoding parameters are required:

| Parameter | Value | Notes |
|-----------|-------|-------|
| Codec | H.264 (libx264) | AVC encoding |
| Profile | **Baseline** | Not High - despite transfer syntax name |
| Level | 5.1 | High definition support |
| Frame Rate | 30 fps | Fixed rate |
| Pixel Format | yuv420p | 4:2:0 chroma subsampling |
| MP4 Brand | **mp42** | Not isom - critical for eUnity |
| Audio | None | Stripped with -an |
| Container | MP4 | moov at end (no faststart) |

### DICOM Structure

**SOP Class**: Video Endoscopic Image Storage (1.2.840.10008.5.1.4.1.1.77.1.1.1)

**Transfer Syntax**: MPEG-4 AVC/H.264 High Profile (1.2.840.10008.1.2.4.102)

**Critical DICOM Tags** (values that differ from typical defaults):
- Rows: 0 (video dimensions in MP4 container)
- Columns: 0
- NumberOfFrames: "0"
- CineRate: "0"
- FrameTime: "0.0" (DS type)
- EffectiveDuration: "0.0" (DS type)
- PhotometricInterpretation: YBR_PARTIAL_420
- LossyImageCompression: "01"

**Pixel Data Encapsulation**:
- Empty offset table (Item 0: length=0)
- Single fragment containing entire MP4 stream (Item 1)
- Sequence delimiter
- Total: 2 items only (not 3)

### Key Implementation Notes

1. **Dataset Transfer Syntax**: Must be set in DicomDataset constructor, not just FileMetaInfo
2. **Fragment Structure**: Do not add extra empty fragments - only offset table + video data
3. **MP4 Brand**: FFmpeg default is `isom`, must explicitly set `-brand mp42`
4. **Even Length**: Pixel data must have even byte length (pad with 0x00 if needed)

## Dependencies

- fo-dicom v5.2.2+
- fo-dicom.Codecs v5.1.6.4+
- fo-dicom.Network v5.2.2+

## FFmpeg Setup

Place FFmpeg binaries in `Resources/FFmpeg/`:
- ffmpeg.exe
- ffprobe.exe

Use GPL build with libx264 support from https://www.gyan.dev/ffmpeg/builds/

## License

Proprietary - All Rights Reserved
