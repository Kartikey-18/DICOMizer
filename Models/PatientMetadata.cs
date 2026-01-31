using System.ComponentModel.DataAnnotations;

namespace DICOMizer.Models;

/// <summary>
/// Represents patient metadata for DICOM file creation
/// </summary>
public class PatientMetadata
{
    [Required(ErrorMessage = "Patient ID is required")]
    [StringLength(64, ErrorMessage = "Patient ID must be 64 characters or less")]
    public string PatientId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Patient Name is required")]
    [StringLength(64, ErrorMessage = "Patient Name must be 64 characters or less")]
    public string PatientName { get; set; } = string.Empty;

    [StringLength(16, ErrorMessage = "Patient Birth Date must be 16 characters or less")]
    public string? PatientBirthDate { get; set; }

    [RegularExpression(@"^[MFO]$", ErrorMessage = "Patient Sex must be M, F, or O")]
    public string? PatientSex { get; set; }

    [StringLength(64, ErrorMessage = "Study Description must be 64 characters or less")]
    public string? StudyDescription { get; set; }

    [StringLength(64, ErrorMessage = "Series Description must be 64 characters or less")]
    public string? SeriesDescription { get; set; }

    [StringLength(64, ErrorMessage = "Physician Name must be 64 characters or less")]
    public string? PerformingPhysicianName { get; set; }

    [StringLength(16, ErrorMessage = "Accession Number must be 16 characters or less")]
    public string? AccessionNumber { get; set; }

    public string Modality { get; set; } = "ES";

    public DateTime StudyDate { get; set; } = DateTime.Now;
    public DateTime SeriesDate { get; set; } = DateTime.Now;
    public DateTime ContentDate { get; set; } = DateTime.Now;

    /// <summary>
    /// Validates all fields and returns validation errors
    /// </summary>
    public IEnumerable<ValidationResult> Validate()
    {
        var context = new ValidationContext(this);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(this, context, results, validateAllProperties: true);
        return results;
    }

    /// <summary>
    /// Checks if the metadata is valid
    /// </summary>
    public bool IsValid()
    {
        return !Validate().Any();
    }

    /// <summary>
    /// Formats patient birth date for DICOM (YYYYMMDD)
    /// </summary>
    public string? GetDicomBirthDate()
    {
        if (string.IsNullOrWhiteSpace(PatientBirthDate))
            return null;

        // Try to parse and reformat if needed
        if (DateTime.TryParse(PatientBirthDate, out var date))
        {
            return date.ToString("yyyyMMdd");
        }

        return PatientBirthDate;
    }

    /// <summary>
    /// Formats date for DICOM (YYYYMMDD)
    /// </summary>
    public static string FormatDicomDate(DateTime date)
    {
        return date.ToString("yyyyMMdd");
    }

    /// <summary>
    /// Formats time for DICOM (HHMMSS.FFFFFF)
    /// </summary>
    public static string FormatDicomTime(DateTime date)
    {
        return date.ToString("HHmmss.ffffff");
    }
}
