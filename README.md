# DICOMizer

A WPF application for converting video files to DICOM format compatible with eUnity PACS systems.

## Features

- Convert video files (MP4, AVI, MOV) to DICOM format
- Video trimming functionality
- Patient metadata entry
- Direct PACS transmission via DICOM C-STORE
- H.264 High@L4.1 encoding for optimal compatibility
- Hardware acceleration support

## Requirements

- .NET 8.0 Runtime
- Windows 10 or later
- FFmpeg (bundled)

## Build Instructions

```bash
dotnet restore
dotnet build
dotnet run
```

## Project Structure

```
DICOMizer/
├── Models/          # Data models
├── Services/        # Business logic services
├── Utilities/       # Helper classes
├── Views/           # WPF UI windows
└── Resources/       # Assets and FFmpeg binaries
```

## Dependencies

- fo-dicom v5.2.2+
- fo-dicom.Codecs v5.1.6.4+
- fo-dicom.Network v5.2.2+

## License

Proprietary - All Rights Reserved
