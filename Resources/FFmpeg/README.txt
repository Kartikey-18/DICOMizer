================================================================================
  FFmpeg Installation Instructions for DICOMizer
================================================================================

DICOMizer requires FFmpeg to process video files. Please follow these steps
to install FFmpeg:

1. DOWNLOAD FFMPEG
   ---------------
   Download FFmpeg for Windows from one of these sources:

   Option A - Official Builds:
   https://ffmpeg.org/download.html
   → Click "Windows" → Choose "Windows builds from gyan.dev"
   → Download "ffmpeg-release-essentials.zip"

   Option B - Direct Download:
   https://github.com/BtbN/FFmpeg-Builds/releases
   → Download "ffmpeg-master-latest-win64-gpl.zip"

2. EXTRACT FILES
   -------------
   Extract the downloaded ZIP file to a temporary location.

3. COPY REQUIRED FILES
   -------------------
   From the extracted folder, navigate to the "bin" subfolder and copy
   these TWO files to THIS directory:

   ✓ ffmpeg.exe
   ✓ ffprobe.exe

   Final structure should look like:
   DICOMizer/
     └── Resources/
         └── FFmpeg/
             ├── ffmpeg.exe    ← Copy here
             ├── ffprobe.exe   ← Copy here
             └── README.txt    ← This file

4. VERIFY INSTALLATION
   -------------------
   After copying the files:
   - Build and run DICOMizer
   - If FFmpeg is found, you'll be able to select and process videos
   - If not found, you'll see a warning message

================================================================================

FILE SIZES (approximate):
- ffmpeg.exe:  ~100-130 MB
- ffprobe.exe: ~100-130 MB

IMPORTANT NOTES:
- These files are NOT included in the repository due to their size
- You need to download them separately for each installation
- Both files are required for full functionality
- Use 64-bit Windows builds only

LICENSING:
FFmpeg is licensed under the GNU Lesser General Public License (LGPL) v2.1+
For more information: https://ffmpeg.org/legal.html

================================================================================

Troubleshooting:
- If DICOMizer doesn't detect FFmpeg, ensure both .exe files are in this folder
- Check that files are not blocked (Right-click → Properties → Unblock)
- Verify you downloaded the Windows 64-bit version
- Ensure you have permission to execute the files

For more help, refer to SETUP.md in the project root directory.

================================================================================
