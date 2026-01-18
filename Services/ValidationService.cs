using DICOMizer.Models;
using DICOMizer.Utilities;

namespace DICOMizer.Services;

/// <summary>
/// Service for validating inputs and enforcing security policies
/// </summary>
public class ValidationService
{
    /// <summary>
    /// Validates a video file for processing
    /// </summary>
    public ValidationResult ValidateVideoFile(string filePath)
    {
        var result = new ValidationResult();

        // Check if file exists
        if (!File.Exists(filePath))
        {
            result.AddError("File not found", "The specified video file does not exist.");
            return result;
        }

        // Validate file path for security
        if (!PathHelper.IsValidPath(filePath))
        {
            result.AddError("Invalid path", "The file path contains invalid characters or patterns.");
            return result;
        }

        // Check file format
        if (!PathHelper.IsValidVideoFile(filePath))
        {
            result.AddError("Invalid format", $"Unsupported video format. Supported formats: {string.Join(", ", Constants.SupportedVideoFormats)}");
            return result;
        }

        // Check file size
        if (!PathHelper.IsValidFileSize(filePath))
        {
            var maxSize = PathHelper.FormatFileSize(Constants.MaxFileSizeBytes);
            result.AddError("File too large", $"Video file exceeds maximum size limit of {maxSize}.");
            return result;
        }

        // Check if file is accessible
        try
        {
            using var stream = File.OpenRead(filePath);
        }
        catch (UnauthorizedAccessException)
        {
            result.AddError("Access denied", "Insufficient permissions to read the video file.");
            return result;
        }
        catch (Exception ex)
        {
            result.AddError("File access error", $"Unable to access the video file: {ex.Message}");
            return result;
        }

        result.IsValid = true;
        return result;
    }

    /// <summary>
    /// Validates patient metadata
    /// </summary>
    public ValidationResult ValidatePatientMetadata(PatientMetadata metadata)
    {
        var result = new ValidationResult();

        var validationResults = metadata.Validate();
        foreach (var validationResult in validationResults)
        {
            result.AddError("Validation error", validationResult.ErrorMessage ?? "Unknown validation error");
        }

        // Additional security checks
        if (ContainsSuspiciousPatterns(metadata.PatientId))
        {
            result.AddError("Security error", "Patient ID contains potentially dangerous characters.");
        }

        if (ContainsSuspiciousPatterns(metadata.PatientName))
        {
            result.AddError("Security error", "Patient Name contains potentially dangerous characters.");
        }

        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    /// <summary>
    /// Validates PACS configuration
    /// </summary>
    public ValidationResult ValidatePacsConfiguration(PacsConfiguration config)
    {
        var result = new ValidationResult();

        var validationResults = config.Validate();
        foreach (var validationResult in validationResults)
        {
            result.AddError("Validation error", validationResult.ErrorMessage ?? "Unknown validation error");
        }

        // Validate host format
        if (!string.IsNullOrWhiteSpace(config.Host))
        {
            if (ContainsSuspiciousPatterns(config.Host))
            {
                result.AddError("Security error", "Host contains potentially dangerous characters.");
            }
        }

        // Validate AE Titles
        if (!IsValidAeTitle(config.AeTitle))
        {
            result.AddError("Invalid AE Title", "AE Title must contain only alphanumeric characters, spaces, and underscores.");
        }

        if (!IsValidAeTitle(config.CalledAeTitle))
        {
            result.AddError("Invalid Called AE Title", "Called AE Title must contain only alphanumeric characters, spaces, and underscores.");
        }

        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    /// <summary>
    /// Validates disk space availability
    /// </summary>
    public ValidationResult ValidateDiskSpace(string path, long requiredBytes)
    {
        var result = new ValidationResult();

        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(path) ?? "C:\\");
            if (drive.AvailableFreeSpace < requiredBytes * 2) // Require 2x for safety
            {
                var required = PathHelper.FormatFileSize(requiredBytes * 2);
                var available = PathHelper.FormatFileSize(drive.AvailableFreeSpace);
                result.AddError("Insufficient disk space", $"Required: {required}, Available: {available}");
                return result;
            }
        }
        catch (Exception ex)
        {
            result.AddError("Disk space check failed", ex.Message);
            return result;
        }

        result.IsValid = true;
        return result;
    }

    /// <summary>
    /// Checks for suspicious patterns that might indicate injection attempts
    /// </summary>
    private bool ContainsSuspiciousPatterns(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        // Check for common injection patterns
        var suspiciousPatterns = new[]
        {
            "..",
            "<script",
            "javascript:",
            "onerror=",
            "onclick=",
            "eval(",
            "cmd.exe",
            "powershell",
            "/bin/",
            "&&",
            "||",
            ";rm ",
            ";del "
        };

        return suspiciousPatterns.Any(pattern =>
            input.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Validates DICOM AE Title format
    /// </summary>
    private bool IsValidAeTitle(string aeTitle)
    {
        if (string.IsNullOrWhiteSpace(aeTitle))
            return false;

        if (aeTitle.Length > 16)
            return false;

        // AE Title should only contain alphanumeric, space, underscore
        return aeTitle.All(c => char.IsLetterOrDigit(c) || c == ' ' || c == '_');
    }
}

/// <summary>
/// Represents validation results
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; } = new();

    public void AddError(string field, string message)
    {
        Errors.Add(new ValidationError { Field = field, Message = message });
        IsValid = false;
    }

    public string GetErrorMessage()
    {
        return string.Join("\n", Errors.Select(e => $"• {e.Message}"));
    }
}

/// <summary>
/// Represents a validation error
/// </summary>
public class ValidationError
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
