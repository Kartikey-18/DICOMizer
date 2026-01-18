namespace DICOMizer.Models;

/// <summary>
/// Represents a video to DICOM conversion job with state tracking
/// </summary>
public class ConversionJob
{
    public string JobId { get; set; } = Guid.NewGuid().ToString();
    public VideoMetadata VideoMetadata { get; set; } = new();
    public PatientMetadata PatientMetadata { get; set; } = new();
    public ConversionState State { get; set; } = ConversionState.Pending;
    public int ProgressPercentage { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }

    // Output options
    public bool SaveToFile { get; set; } = true;
    public bool SendToPacs { get; set; } = false;
    public string? OutputFilePath { get; set; }

    // Processing paths
    public string? TempVideoPath { get; set; }
    public string? ProcessedVideoPath { get; set; }

    // Cancellation
    public CancellationTokenSource? CancellationTokenSource { get; set; }

    public TimeSpan? Duration => CompletedAt - StartedAt;

    public bool IsCompleted => State == ConversionState.Completed;
    public bool IsFailed => State == ConversionState.Failed;
    public bool IsCancelled => State == ConversionState.Cancelled;
    public bool IsInProgress => State == ConversionState.Processing;

    public void UpdateProgress(int percentage, string message)
    {
        ProgressPercentage = Math.Clamp(percentage, 0, 100);
        StatusMessage = message;
    }

    public void MarkAsStarted()
    {
        State = ConversionState.Processing;
        StartedAt = DateTime.Now;
        ProgressPercentage = 0;
    }

    public void MarkAsCompleted(string? outputPath = null)
    {
        State = ConversionState.Completed;
        CompletedAt = DateTime.Now;
        ProgressPercentage = 100;
        StatusMessage = "Conversion completed successfully";
        if (!string.IsNullOrEmpty(outputPath))
        {
            OutputFilePath = outputPath;
        }
    }

    public void MarkAsFailed(string errorMessage, Exception? exception = null)
    {
        State = ConversionState.Failed;
        CompletedAt = DateTime.Now;
        ErrorMessage = errorMessage;
        Exception = exception;
        StatusMessage = $"Failed: {errorMessage}";
    }

    public void MarkAsCancelled()
    {
        State = ConversionState.Cancelled;
        CompletedAt = DateTime.Now;
        StatusMessage = "Conversion cancelled";
    }

    public void Cleanup()
    {
        // Clean up temporary files
        try
        {
            if (!string.IsNullOrEmpty(TempVideoPath) && File.Exists(TempVideoPath))
            {
                File.Delete(TempVideoPath);
            }

            if (!string.IsNullOrEmpty(ProcessedVideoPath) && File.Exists(ProcessedVideoPath))
            {
                File.Delete(ProcessedVideoPath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }

        CancellationTokenSource?.Dispose();
    }
}

/// <summary>
/// Represents the state of a conversion job
/// </summary>
public enum ConversionState
{
    Pending,
    Validating,
    Processing,
    Completed,
    Failed,
    Cancelled
}
