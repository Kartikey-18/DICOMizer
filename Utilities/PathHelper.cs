using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DICOMizer.Utilities;

/// <summary>
/// Helper class for file path management and validation
/// </summary>
public static class PathHelper
{
    /// <summary>
    /// Validates a file path to prevent directory traversal attacks
    /// </summary>
    public static bool IsValidPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            var fullPath = Path.GetFullPath(path);

            // Check for invalid characters
            if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                return false;

            // Check for directory traversal attempts
            if (path.Contains(".."))
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sanitizes a filename by removing invalid characters
    /// </summary>
    public static string SanitizeFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return "unnamed";

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new StringBuilder();

        foreach (var c in filename)
        {
            if (!invalidChars.Contains(c))
            {
                sanitized.Append(c);
            }
            else
            {
                sanitized.Append('_');
            }
        }

        var result = sanitized.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? "unnamed" : result;
    }

    /// <summary>
    /// Generates a unique temporary file path
    /// </summary>
    public static string GetTempFilePath(string extension)
    {
        var tempDir = Constants.TempPath;
        Directory.CreateDirectory(tempDir);

        var filename = $"{Guid.NewGuid()}{extension}";
        return Path.Combine(tempDir, filename);
    }

    /// <summary>
    /// Generates an output filename for DICOM files
    /// </summary>
    public static string GenerateDicomFilename(string patientId, string patientName, DateTime studyDate)
    {
        var sanitizedPatientId = SanitizeFilename(patientId);
        var sanitizedPatientName = SanitizeFilename(patientName);
        var dateStr = studyDate.ToString("yyyyMMdd_HHmmss");

        return $"DICOM_{sanitizedPatientId}_{sanitizedPatientName}_{dateStr}.dcm";
    }

    /// <summary>
    /// Gets the output file path for a DICOM file
    /// </summary>
    public static string GetOutputFilePath(string patientId, string patientName, DateTime studyDate)
    {
        var filename = GenerateDicomFilename(patientId, patientName, studyDate);
        var outputDir = Constants.DefaultOutputPath;
        Directory.CreateDirectory(outputDir);

        var filePath = Path.Combine(outputDir, filename);

        // Handle file name conflicts
        if (File.Exists(filePath))
        {
            var counter = 1;
            var nameWithoutExt = Path.GetFileNameWithoutExtension(filename);
            var extension = Path.GetExtension(filename);

            do
            {
                filename = $"{nameWithoutExt}_{counter}{extension}";
                filePath = Path.Combine(outputDir, filename);
                counter++;
            }
            while (File.Exists(filePath));
        }

        return filePath;
    }

    /// <summary>
    /// Validates video file format
    /// </summary>
    public static bool IsValidVideoFile(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return Constants.SupportedVideoFormats.Contains(extension);
    }

    /// <summary>
    /// Validates file size
    /// </summary>
    public static bool IsValidFileSize(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        var fileInfo = new FileInfo(filePath);
        return fileInfo.Length <= Constants.MaxFileSizeBytes;
    }

    /// <summary>
    /// Gets file size in bytes
    /// </summary>
    public static long GetFileSize(string filePath)
    {
        if (!File.Exists(filePath))
            return 0;

        var fileInfo = new FileInfo(filePath);
        return fileInfo.Length;
    }

    /// <summary>
    /// Formats file size for display
    /// </summary>
    public static string FormatFileSize(long bytes)
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        if (bytes >= GB)
            return $"{bytes / (double)GB:F2} GB";
        if (bytes >= MB)
            return $"{bytes / (double)MB:F2} MB";
        if (bytes >= KB)
            return $"{bytes / (double)KB:F2} KB";

        return $"{bytes} bytes";
    }

    /// <summary>
    /// Ensures a directory exists
    /// </summary>
    public static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    /// <summary>
    /// Safely deletes a file
    /// </summary>
    public static bool TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Cleans up old temporary files
    /// </summary>
    public static void CleanupTempFiles(TimeSpan maxAge)
    {
        try
        {
            var tempDir = Constants.TempPath;
            if (!Directory.Exists(tempDir))
                return;

            var files = Directory.GetFiles(tempDir);
            var cutoffTime = DateTime.Now - maxAge;

            foreach (var file in files)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime < cutoffTime)
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // Ignore errors when cleaning up individual files
                }
            }
        }
        catch
        {
            // Ignore errors when cleaning up temp directory
        }
    }

    /// <summary>
    /// Opens a folder in Windows Explorer
    /// </summary>
    public static void OpenFolderInExplorer(string folderPath)
    {
        if (Directory.Exists(folderPath))
        {
            System.Diagnostics.Process.Start("explorer.exe", folderPath);
        }
    }

    /// <summary>
    /// Opens a file in Windows Explorer (selects the file)
    /// </summary>
    public static void OpenFileInExplorer(string filePath)
    {
        if (File.Exists(filePath))
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
        }
    }
}
