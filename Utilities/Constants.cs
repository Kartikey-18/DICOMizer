using System.IO;

namespace DICOMizer.Utilities;

/// <summary>
/// Application-wide constants
/// </summary>
public static class Constants
{
    // Application Info
    public const string AppName = "DICOMizer";
    public const string AppVersion = "1.0.0";
    public const string Manufacturer = "Healthcare Solutions";

    // DICOM UIDs
    public const string ImplementationClassUid = "1.2.826.0.1.3680043.10.1091";
    public const string ImplementationVersionName = "DICOMIZER_100";

    // Video Endoscopic Image Storage SOP Class
    public const string VideoEndoscopicSopClassUid = "1.2.840.10008.5.1.4.1.1.77.1.1.1";

    // Transfer Syntax UIDs
    public const string Mpeg4TransferSyntaxUid = "1.2.840.10008.1.2.4.102"; // MPEG-4 AVC/H.264 High Profile
    public const string ExplicitVRLittleEndianUid = "1.2.840.10008.1.2.1";

    // Video Processing Settings
    public const int DefaultFrameRate = 30; // 30fps for eUnity compatibility per design doc
    public const string DefaultVideoCodec = "libx264";
    // Fallback encoders to try if libx264 is not available
    public static readonly string[] FallbackVideoEncoders = { "h264_nvenc", "h264_qsv", "h264_amf", "h264_mf" };
    public const string DefaultVideoProfile = "high";
    public const string DefaultVideoLevel = "4.1";
    public const string DefaultPixelFormat = "yuv420p";
    public const int DefaultCrf = 23; // Constant Rate Factor (lower = better quality)

    // H.264 Encoding Settings
    public const int MaxWidth = 1920;
    public const int MaxHeight = 1080;
    public const string H264Preset = "medium"; // faster, fast, medium, slow, slower

    // DICOM Fragment Size
    public const int DicomFragmentSize = 256 * 1024; // 256 KB chunks

    // File Validation
    public const long MaxFileSizeBytes = 5L * 1024 * 1024 * 1024; // 5 GB
    public static readonly string[] SupportedVideoFormats = { ".mp4", ".avi", ".mov", ".mkv", ".wmv", ".flv", ".m4v" };

    // Paths
    public static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppName
    );

    public static readonly string LogsPath = Path.Combine(AppDataPath, "Logs");
    public static readonly string TempPath = Path.Combine(AppDataPath, "Temp");
    public static readonly string SettingsPath = Path.Combine(AppDataPath, "settings.json");

    public static readonly string DefaultOutputPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads",
        "DICOM"
    );

    // FFmpeg Settings
    public static readonly string FFmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "FFmpeg", "ffmpeg.exe");
    public static readonly string FFprobePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "FFmpeg", "ffprobe.exe");

    // Logging
    public const int LogRetentionDays = 30;
    public const long MaxLogFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    // PACS
    public const int DefaultPacsPort = 104;
    public const int DefaultPacsTimeout = 30;

    // UI
    public const int ProgressUpdateIntervalMs = 100;
    public const int VideoPreviewWidth = 640;
    public const int VideoPreviewHeight = 480;

    // DICOM Tags (for reference)
    public static class DicomTags
    {
        public const string PatientName = "00100010";
        public const string PatientId = "00100020";
        public const string PatientBirthDate = "00100030";
        public const string PatientSex = "00100040";
        public const string StudyDate = "00080020";
        public const string StudyTime = "00080030";
        public const string SeriesDate = "00080021";
        public const string SeriesTime = "00080031";
        public const string ContentDate = "00080023";
        public const string ContentTime = "00080033";
        public const string Modality = "00080060";
        public const string StudyDescription = "00081030";
        public const string SeriesDescription = "0008103E";
        public const string PerformingPhysicianName = "00081050";
        public const string Manufacturer = "00080070";
        public const string SopClassUid = "00080016";
        public const string SopInstanceUid = "00080018";
        public const string StudyInstanceUid = "0020000D";
        public const string SeriesInstanceUid = "0020000E";
        public const string FrameOfReferenceUid = "00200052";
    }

    // Error Messages
    public static class ErrorMessages
    {
        public const string FileNotFound = "The specified video file was not found.";
        public const string FileTooLarge = "The video file exceeds the maximum size limit of 5 GB.";
        public const string UnsupportedFormat = "The video format is not supported.";
        public const string InvalidPatientData = "Patient metadata is incomplete or invalid.";
        public const string FFmpegNotFound = "FFmpeg executable not found. Please ensure it is installed.";
        public const string ConversionFailed = "Video conversion failed. Please check the video file.";
        public const string DicomCreationFailed = "Failed to create DICOM file.";
        public const string PacsConnectionFailed = "Failed to connect to PACS server.";
        public const string PacsTransmissionFailed = "Failed to transmit DICOM file to PACS.";
    }

    /// <summary>
    /// Ensures all required directories exist
    /// </summary>
    public static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(AppDataPath);
        Directory.CreateDirectory(LogsPath);
        Directory.CreateDirectory(TempPath);
        Directory.CreateDirectory(DefaultOutputPath);
    }
}
