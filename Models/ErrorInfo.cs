using System.IO;

namespace DICOMizer.Models;

/// <summary>
/// Represents error information with user-friendly messages
/// </summary>
public class ErrorInfo
{
    public ErrorType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? TechnicalDetails { get; set; }
    public Exception? Exception { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.Now;

    public string GetUserMessage()
    {
        var baseMessage = Type switch
        {
            ErrorType.FileNotFound => "The specified file could not be found.",
            ErrorType.InvalidFormat => "The file format is not supported or invalid.",
            ErrorType.FileTooLarge => "The file exceeds the maximum size limit.",
            ErrorType.ValidationError => "Validation failed. Please check your inputs.",
            ErrorType.FFmpegError => "Video processing failed. Please check the video file.",
            ErrorType.DicomError => "Failed to create DICOM file.",
            ErrorType.PacsConnectionError => "Unable to connect to PACS server.",
            ErrorType.PacsTransmissionError => "Failed to send DICOM file to PACS.",
            ErrorType.NetworkError => "Network communication failed.",
            ErrorType.PermissionError => "Insufficient permissions to access the file or directory.",
            ErrorType.DiskSpaceError => "Insufficient disk space.",
            ErrorType.ConfigurationError => "Configuration error. Please check settings.",
            ErrorType.Unknown => "An unexpected error occurred.",
            _ => "An error occurred."
        };

        return string.IsNullOrWhiteSpace(Message) ? baseMessage : $"{baseMessage}\n\n{Message}";
    }

    public static ErrorInfo FromException(Exception ex, ErrorType? type = null)
    {
        var errorType = type ?? DetermineErrorType(ex);

        return new ErrorInfo
        {
            Type = errorType,
            Message = ex.Message,
            TechnicalDetails = ex.StackTrace,
            Exception = ex
        };
    }

    private static ErrorType DetermineErrorType(Exception ex)
    {
        return ex switch
        {
            FileNotFoundException => ErrorType.FileNotFound,
            DirectoryNotFoundException => ErrorType.FileNotFound,
            UnauthorizedAccessException => ErrorType.PermissionError,
            IOException when ex.Message.Contains("disk") => ErrorType.DiskSpaceError,
            IOException => ErrorType.NetworkError,
            InvalidOperationException when ex.Message.Contains("FFmpeg") => ErrorType.FFmpegError,
            InvalidOperationException when ex.Message.Contains("DICOM") => ErrorType.DicomError,
            InvalidOperationException when ex.Message.Contains("PACS") => ErrorType.PacsConnectionError,
            TimeoutException => ErrorType.NetworkError,
            _ => ErrorType.Unknown
        };
    }
}

/// <summary>
/// Types of errors that can occur
/// </summary>
public enum ErrorType
{
    Unknown,
    FileNotFound,
    InvalidFormat,
    FileTooLarge,
    ValidationError,
    FFmpegError,
    DicomError,
    PacsConnectionError,
    PacsTransmissionError,
    NetworkError,
    PermissionError,
    DiskSpaceError,
    ConfigurationError
}
