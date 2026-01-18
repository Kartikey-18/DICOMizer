using System.ComponentModel.DataAnnotations;

namespace DICOMizer.Models;

/// <summary>
/// Represents PACS server configuration
/// </summary>
public class PacsConfiguration
{
    [Required(ErrorMessage = "PACS Host is required")]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    public int Port { get; set; } = 104;

    [Required(ErrorMessage = "AE Title is required")]
    [StringLength(16, ErrorMessage = "AE Title must be 16 characters or less")]
    public string AeTitle { get; set; } = "DICOMIZER";

    [Required(ErrorMessage = "Called AE Title is required")]
    [StringLength(16, ErrorMessage = "Called AE Title must be 16 characters or less")]
    public string CalledAeTitle { get; set; } = "EUNITY";

    public int TimeoutSeconds { get; set; } = 30;

    public bool UseTls { get; set; } = false;

    /// <summary>
    /// Validates the configuration
    /// </summary>
    public IEnumerable<ValidationResult> Validate()
    {
        var context = new ValidationContext(this);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(this, context, results, validateAllProperties: true);
        return results;
    }

    /// <summary>
    /// Checks if the configuration is valid
    /// </summary>
    public bool IsValid()
    {
        return !Validate().Any();
    }

    /// <summary>
    /// Gets a display string for the PACS configuration
    /// </summary>
    public string GetDisplayString()
    {
        return $"{AeTitle} -> {CalledAeTitle}@{Host}:{Port}";
    }

    /// <summary>
    /// Creates a deep copy of the configuration
    /// </summary>
    public PacsConfiguration Clone()
    {
        return new PacsConfiguration
        {
            Host = this.Host,
            Port = this.Port,
            AeTitle = this.AeTitle,
            CalledAeTitle = this.CalledAeTitle,
            TimeoutSeconds = this.TimeoutSeconds,
            UseTls = this.UseTls
        };
    }
}
