# DICOMizer - Deployment Checklist

Use this checklist to ensure successful deployment of DICOMizer to end users.

---

## Pre-Deployment Checklist

### 1. Prerequisites ✅

- [ ] Visual Studio 2022 installed
- [ ] .NET 8.0 SDK installed
- [ ] FFmpeg binaries downloaded
- [ ] All source code committed to repository

### 2. FFmpeg Installation ⚠️

- [ ] Download FFmpeg for Windows (64-bit)
  - Source: https://ffmpeg.org/download.html
  - File: ffmpeg-release-essentials.zip or similar
- [ ] Extract FFmpeg archive
- [ ] Copy `ffmpeg.exe` to `Resources/FFmpeg/`
- [ ] Copy `ffprobe.exe` to `Resources/FFmpeg/`
- [ ] Verify file sizes:
  - ffmpeg.exe: ~100-130 MB
  - ffprobe.exe: ~100-130 MB

### 3. Build Verification ✅

- [ ] Clean solution: `dotnet clean`
- [ ] Restore packages: `dotnet restore`
- [ ] Build in Debug mode: `dotnet build -c Debug`
- [ ] Build in Release mode: `dotnet build -c Release`
- [ ] No build errors or warnings
- [ ] FFmpeg files copied to output directory

---

## Testing Checklist

### 4. Functional Testing 🧪

#### Video Processing
- [ ] Select and load MP4 file
- [ ] Select and load AVI file
- [ ] Select and load MOV file
- [ ] Video metadata displays correctly (resolution, duration, fps, size)
- [ ] Video preview shows in MediaElement
- [ ] Video plays and pauses correctly

#### Trimming
- [ ] Open Trim window
- [ ] Play/pause video
- [ ] Navigate timeline with slider
- [ ] Set start time from current position
- [ ] Set end time from current position
- [ ] Enter manual time (HH:MM:SS)
- [ ] Jump to start marker
- [ ] Jump to end marker
- [ ] Previous/Next frame navigation works
- [ ] Trimmed duration calculates correctly
- [ ] Apply trim returns to main window

#### Patient Metadata
- [ ] Patient ID required validation works
- [ ] Patient Name required validation works
- [ ] Optional fields accept input
- [ ] Birth date picker works
- [ ] Sex dropdown works
- [ ] Invalid data shows error message

#### Conversion
- [ ] Convert without trimming works
- [ ] Convert with trimming works
- [ ] Progress bar updates during conversion
- [ ] Status messages display correctly
- [ ] Cancel button stops conversion
- [ ] DICOM file created in output folder
- [ ] Output file opens in DICOM viewer

#### Settings
- [ ] Settings window opens
- [ ] PACS configuration fields accept input
- [ ] Settings save to %APPDATA%\DICOMizer\
- [ ] Settings load on app restart
- [ ] Test Connection button works (with valid PACS)
- [ ] Application settings (auto-open, hardware accel) save

#### PACS (if available)
- [ ] Configure PACS settings
- [ ] Test Connection succeeds
- [ ] Send to PACS checkbox works
- [ ] DICOM file transmits successfully
- [ ] PACS receives and displays file

### 5. Error Handling 🛡️

- [ ] Invalid file format shows error
- [ ] File too large (>5GB) shows error
- [ ] Missing FFmpeg shows warning
- [ ] Missing Patient ID shows validation error
- [ ] PACS connection failure shows error
- [ ] Disk space error handled gracefully
- [ ] All errors logged to file

### 6. Security Validation 🔒

- [ ] Path traversal attempts blocked
- [ ] File path validation works
- [ ] Input sanitization active
- [ ] AE Title validation enforces alphanumeric
- [ ] No injection vulnerabilities
- [ ] Temporary files cleaned up after conversion

### 7. Logging & Monitoring 📝

- [ ] Log file created in %APPDATA%\DICOMizer\Logs\
- [ ] Timestamp format correct
- [ ] Log levels (Info, Warning, Error) working
- [ ] Exceptions logged with stack trace
- [ ] Log rotation works (daily)
- [ ] Old logs deleted after 30 days

---

## Deployment Preparation

### 8. Build Release Package 📦

#### Option A: Standard Build
```powershell
cd DICOMizer
.\build.ps1 -Configuration Release -All
```

#### Option B: Self-Contained
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish/Release
```

- [ ] Build completed without errors
- [ ] All DLLs present in output directory
- [ ] FFmpeg binaries in output directory
- [ ] No debug symbols in release build

### 9. Package Contents Verification 📋

Check that the following are in the publish folder:

**Required Files:**
- [ ] DICOMizer.exe
- [ ] DICOMizer.dll (if not single file)
- [ ] fo-dicom.dll
- [ ] fo-dicom.Codecs.dll
- [ ] fo-dicom.Network.dll
- [ ] All required .NET runtime DLLs
- [ ] Resources/FFmpeg/ffmpeg.exe
- [ ] Resources/FFmpeg/ffprobe.exe

**Documentation:**
- [ ] README.md
- [ ] QUICKSTART.md
- [ ] SETUP.md
- [ ] Resources/FFmpeg/README.txt

### 10. Installation Testing 🖥️

#### Clean Machine Test
- [ ] Test on clean Windows 10 machine
- [ ] Test on clean Windows 11 machine
- [ ] .NET 8.0 Runtime installs correctly
- [ ] Application launches without errors
- [ ] All features work on clean install

#### Permissions Test
- [ ] Application runs without admin rights
- [ ] Can create files in output directory
- [ ] Can write to %APPDATA% folder
- [ ] Can read/write settings file
- [ ] Can create log files

---

## Documentation Checklist

### 11. User Documentation 📚

- [ ] QUICKSTART.md reviewed and accurate
- [ ] SETUP.md reviewed and accurate
- [ ] Screenshot workflow documented
- [ ] Common errors documented
- [ ] PACS configuration guide complete
- [ ] FFmpeg installation guide complete

### 12. Technical Documentation 📖

- [ ] PROJECT_STATUS.md updated
- [ ] IMPLEMENTATION_SUMMARY.md complete
- [ ] Code comments accurate
- [ ] Architecture documented
- [ ] Dependencies listed

---

## Deployment Execution

### 13. Distribution 🚀

#### Method 1: Manual Deployment
- [ ] Copy publish folder to target machine
- [ ] Create desktop shortcut
- [ ] Add to Start Menu
- [ ] Verify all files present

#### Method 2: ZIP Distribution
- [ ] Create ZIP of publish folder
- [ ] Include README in root
- [ ] Test extraction on target machine
- [ ] Verify no missing files

#### Method 3: Installer (Future)
- [ ] Create ClickOnce package
- [ ] Or create WiX installer
- [ ] Include .NET runtime installer
- [ ] Test installation process

### 14. User Training 👥

- [ ] Provide QUICKSTART guide
- [ ] Demonstrate video selection
- [ ] Show patient metadata entry
- [ ] Explain trimming feature
- [ ] Configure PACS settings
- [ ] Test end-to-end workflow
- [ ] Review error messages
- [ ] Show log file location

---

## Post-Deployment Checklist

### 15. Initial Deployment Verification ✅

- [ ] Application launches on target machine
- [ ] Can select and analyze video file
- [ ] Video preview works
- [ ] Trimming functionality works
- [ ] Patient metadata entry works
- [ ] DICOM creation succeeds
- [ ] Output file valid
- [ ] PACS transmission works (if configured)

### 16. User Acceptance Testing 👤

- [ ] User can complete basic workflow
- [ ] User understands error messages
- [ ] User can find output files
- [ ] User can configure PACS
- [ ] User satisfied with performance
- [ ] User feedback collected

### 17. Monitoring & Support 📊

- [ ] Establish log file collection process
- [ ] Create support ticket system
- [ ] Document common issues
- [ ] Monitor PACS connectivity
- [ ] Track conversion success rate
- [ ] Collect user feedback

---

## Rollback Plan

### 18. Rollback Preparation 🔄

In case of deployment issues:

- [ ] Keep previous version available
- [ ] Document rollback procedure
- [ ] Test rollback process
- [ ] Backup user settings before upgrade
- [ ] Communication plan for users

---

## Final Sign-Off

### Pre-Production Checklist Summary

| Category | Status | Notes |
|----------|--------|-------|
| Prerequisites | ⬜ | |
| FFmpeg Installation | ⬜ | |
| Build Verification | ⬜ | |
| Functional Testing | ⬜ | |
| Error Handling | ⬜ | |
| Security | ⬜ | |
| Logging | ⬜ | |
| Package Build | ⬜ | |
| Clean Install Test | ⬜ | |
| Documentation | ⬜ | |
| User Training | ⬜ | |

### Sign-Off

- [ ] **Developer**: Code complete and tested
- [ ] **QA**: All tests passed
- [ ] **IT**: Deployment package verified
- [ ] **User**: Training complete and accepted
- [ ] **Manager**: Approved for production

---

## Quick Reference

### Essential Commands

**Build:**
```powershell
.\build.ps1 -All -Configuration Release
```

**Run:**
```powershell
cd bin/Release/net8.0-windows
./DICOMizer.exe
```

**Clean:**
```powershell
dotnet clean
```

**Restore:**
```powershell
dotnet restore
```

### Critical Paths

```
Settings:   %APPDATA%\DICOMizer\settings.json
Logs:       %APPDATA%\DICOMizer\Logs\
Temp:       %APPDATA%\DICOMizer\Temp\
Output:     %USERPROFILE%\Downloads\DICOM\
```

### Support Contacts

- **Technical Issues**: [Your IT Support]
- **PACS Configuration**: [PACS Administrator]
- **Application Bugs**: [Development Team]

---

## Notes

**Deployment Date**: _______________
**Deployed By**: _______________
**Version**: 1.0.0
**Target Environment**: Production / Staging / Testing

**Special Instructions**:
_________________________________________
_________________________________________
_________________________________________

---

**Document Version**: 1.0
**Last Updated**: November 24, 2025
