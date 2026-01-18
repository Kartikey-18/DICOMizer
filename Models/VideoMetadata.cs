namespace DICOMizer.Models;

/// <summary>
/// Represents metadata extracted from a video file
/// </summary>
public class VideoMetadata
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public TimeSpan Duration { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double FrameRate { get; set; }
    public string CodecName { get; set; } = string.Empty;
    public string PixelFormat { get; set; } = string.Empty;
    public long BitRate { get; set; }

    // Trimming information
    public TimeSpan? TrimStart { get; set; }
    public TimeSpan? TrimEnd { get; set; }

    public bool IsTrimmed => TrimStart.HasValue || TrimEnd.HasValue;

    public TimeSpan EffectiveDuration
    {
        get
        {
            var start = TrimStart ?? TimeSpan.Zero;
            var end = TrimEnd ?? Duration;
            return end - start;
        }
    }

    public string GetResolutionString() => $"{Width}x{Height}";

    public string GetFileSizeString()
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        if (FileSizeBytes >= GB)
            return $"{FileSizeBytes / (double)GB:F2} GB";
        if (FileSizeBytes >= MB)
            return $"{FileSizeBytes / (double)MB:F2} MB";
        if (FileSizeBytes >= KB)
            return $"{FileSizeBytes / (double)KB:F2} KB";

        return $"{FileSizeBytes} bytes";
    }
}
