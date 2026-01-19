using System.IO;
using DICOMizer.Models;
using DICOMizer.Utilities;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DICOMizer.Services;

/// <summary>
/// Service for video processing operations using FFmpeg
/// </summary>
public class VideoProcessingService
{
    private readonly ProcessRunner _processRunner;

    public VideoProcessingService()
    {
        _processRunner = new ProcessRunner();
    }

    /// <summary>
    /// Analyzes a video file and extracts metadata using FFprobe
    /// </summary>
    public async Task<VideoMetadata?> AnalyzeVideoAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Video file not found", filePath);

        if (!PathHelper.IsValidVideoFile(filePath))
            throw new InvalidOperationException("Invalid video file format");

        // Check file size
        if (!PathHelper.IsValidFileSize(filePath))
            throw new InvalidOperationException($"Video file exceeds maximum size of {PathHelper.FormatFileSize(Constants.MaxFileSizeBytes)}");

        var arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"";

        var result = await _processRunner.RunAsync(
            Constants.FFprobePath,
            arguments,
            cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException($"FFprobe failed: {result.StandardError}");
        }

        return ParseFFprobeOutput(result.StandardOutput, filePath);
    }

    /// <summary>
    /// Parses FFprobe JSON output
    /// </summary>
    private VideoMetadata? ParseFFprobeOutput(string jsonOutput, string filePath)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonOutput);
            var root = doc.RootElement;

            // Get video stream
            var videoStream = root.GetProperty("streams")
                .EnumerateArray()
                .FirstOrDefault(s => s.GetProperty("codec_type").GetString() == "video");

            if (videoStream.ValueKind == JsonValueKind.Undefined)
                return null;

            var format = root.GetProperty("format");

            var metadata = new VideoMetadata
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                FileSizeBytes = PathHelper.GetFileSize(filePath)
            };

            // Parse duration
            if (format.TryGetProperty("duration", out var durationProp))
            {
                if (double.TryParse(durationProp.GetString(), out var durationSeconds))
                {
                    metadata.Duration = TimeSpan.FromSeconds(durationSeconds);
                }
            }

            // Parse dimensions
            if (videoStream.TryGetProperty("width", out var widthProp))
                metadata.Width = widthProp.GetInt32();

            if (videoStream.TryGetProperty("height", out var heightProp))
                metadata.Height = heightProp.GetInt32();

            // Parse frame rate
            if (videoStream.TryGetProperty("r_frame_rate", out var frameRateProp))
            {
                var frameRateStr = frameRateProp.GetString();
                if (!string.IsNullOrEmpty(frameRateStr) && frameRateStr.Contains('/'))
                {
                    var parts = frameRateStr.Split('/');
                    if (parts.Length == 2 &&
                        double.TryParse(parts[0], out var num) &&
                        double.TryParse(parts[1], out var den) &&
                        den != 0)
                    {
                        metadata.FrameRate = num / den;
                    }
                }
            }

            // Parse codec
            if (videoStream.TryGetProperty("codec_name", out var codecProp))
                metadata.CodecName = codecProp.GetString() ?? string.Empty;

            // Parse pixel format
            if (videoStream.TryGetProperty("pix_fmt", out var pixFmtProp))
                metadata.PixelFormat = pixFmtProp.GetString() ?? string.Empty;

            // Parse bitrate
            if (format.TryGetProperty("bit_rate", out var bitRateProp))
            {
                if (long.TryParse(bitRateProp.GetString(), out var bitRate))
                {
                    metadata.BitRate = bitRate;
                }
            }

            return metadata;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse FFprobe output: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Trims a video file using FFmpeg with stream copy (no re-encoding)
    /// </summary>
    public async Task<string> TrimVideoAsync(
        string inputPath,
        TimeSpan startTime,
        TimeSpan endTime,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var outputPath = PathHelper.GetTempFilePath(Path.GetExtension(inputPath));
        var duration = endTime - startTime;

        var arguments = $"-i \"{inputPath}\" -ss {FormatTimeSpan(startTime)} -t {FormatTimeSpan(duration)} -c copy \"{outputPath}\"";

        var result = await _processRunner.RunFFmpegAsync(
            arguments,
            duration,
            progress,
            cancellationToken);

        if (!result.Success || !File.Exists(outputPath))
        {
            PathHelper.TryDeleteFile(outputPath);
            throw new InvalidOperationException($"Video trimming failed: {result.StandardError}");
        }

        return outputPath;
    }

    /// <summary>
    /// Transcodes a video to H.264 High@L4.1 for DICOM compatibility
    /// </summary>
    public async Task<string> TranscodeToH264Async(
        string inputPath,
        VideoMetadata metadata,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var outputPath = PathHelper.GetTempFilePath(".264");

        // Calculate output dimensions (maintain aspect ratio, max 1920x1080)
        var (width, height) = CalculateOutputDimensions(metadata.Width, metadata.Height);

        // Build FFmpeg arguments for H.264 High@L4.1 encoding
        var arguments = BuildTranscodeArguments(inputPath, outputPath, width, height);

        var result = await _processRunner.RunFFmpegAsync(
            arguments,
            metadata.EffectiveDuration,
            progress,
            cancellationToken);

        if (!result.Success || !File.Exists(outputPath))
        {
            PathHelper.TryDeleteFile(outputPath);
            throw new InvalidOperationException($"Video transcoding failed: {result.StandardError}");
        }

        return outputPath;
    }

    /// <summary>
    /// Builds FFmpeg transcode arguments for DICOM-compatible H.264
    /// Outputs raw H.264 Annex-B bitstream as required by DICOM MPEG-4 transfer syntax
    /// </summary>
    private string BuildTranscodeArguments(string inputPath, string outputPath, int width, int height)
    {
        // H.264 High@L4.1 encoding per design document
        // Output raw H.264 Annex-B format for DICOM encapsulation
        // Use baseline features that ensure maximum compatibility
        var args = new List<string>
        {
            "-i", $"\"{inputPath}\"",
            "-c:v", "libx264",
            "-profile:v", "high",
            "-level", "4.1",
            "-r", "30",
            "-pix_fmt", "yuv420p",
            "-g", "30",              // GOP size = 1 second (30 frames at 30fps)
            "-bf", "0",              // No B-frames for simpler decoding
            "-an",
            "-bsf:v", "h264_mp4toannexb",  // Ensure Annex-B format with start codes
            "-f", "h264",
            "-y",
            $"\"{outputPath}\""
        };

        return string.Join(" ", args);
    }

    /// <summary>
    /// Calculates output dimensions maintaining aspect ratio
    /// </summary>
    private (int width, int height) CalculateOutputDimensions(int inputWidth, int inputHeight)
    {
        if (inputWidth <= Constants.MaxWidth && inputHeight <= Constants.MaxHeight)
            return (inputWidth, inputHeight);

        var aspectRatio = (double)inputWidth / inputHeight;

        int outputWidth, outputHeight;

        if (inputWidth > inputHeight)
        {
            // Landscape
            outputWidth = Constants.MaxWidth;
            outputHeight = (int)(outputWidth / aspectRatio);

            // Ensure height is even (required for H.264)
            if (outputHeight % 2 != 0)
                outputHeight--;
        }
        else
        {
            // Portrait or square
            outputHeight = Constants.MaxHeight;
            outputWidth = (int)(outputHeight * aspectRatio);

            // Ensure width is even
            if (outputWidth % 2 != 0)
                outputWidth--;
        }

        return (outputWidth, outputHeight);
    }

    /// <summary>
    /// Checks if FFmpeg and FFprobe are available
    /// </summary>
    public bool CheckFFmpegAvailable()
    {
        return File.Exists(Constants.FFmpegPath) && File.Exists(Constants.FFprobePath);
    }

    /// <summary>
    /// Formats TimeSpan for FFmpeg
    /// </summary>
    private string FormatTimeSpan(TimeSpan timeSpan)
    {
        return $"{(int)timeSpan.TotalHours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}.{timeSpan.Milliseconds:D3}";
    }
}
