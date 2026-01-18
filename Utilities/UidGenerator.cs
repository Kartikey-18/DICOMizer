using System.Security.Cryptography;
using System.Text;

namespace DICOMizer.Utilities;

/// <summary>
/// Generates unique DICOM UIDs
/// </summary>
public static class UidGenerator
{
    private static readonly string OrgRoot = Constants.ImplementationClassUid;
    private static readonly object LockObject = new();
    private static long _counter = 0;

    /// <summary>
    /// Generates a new unique DICOM UID
    /// </summary>
    public static string Generate()
    {
        lock (LockObject)
        {
            // Format: {OrgRoot}.{Timestamp}.{Counter}.{Random}
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _counter = (_counter + 1) % 10000;

            // Generate a random component for additional uniqueness
            var randomBytes = new byte[4];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            var random = BitConverter.ToUInt32(randomBytes, 0) % 100000;

            var uid = $"{OrgRoot}.{timestamp}.{_counter}.{random}";

            // Ensure UID doesn't exceed 64 characters (DICOM limit)
            if (uid.Length > 64)
            {
                uid = uid.Substring(0, 64);
            }

            return uid;
        }
    }

    /// <summary>
    /// Generates a Study Instance UID
    /// </summary>
    public static string GenerateStudyUid()
    {
        return Generate();
    }

    /// <summary>
    /// Generates a Series Instance UID
    /// </summary>
    public static string GenerateSeriesUid()
    {
        return Generate();
    }

    /// <summary>
    /// Generates a SOP Instance UID
    /// </summary>
    public static string GenerateSopInstanceUid()
    {
        return Generate();
    }

    /// <summary>
    /// Generates a Frame of Reference UID
    /// </summary>
    public static string GenerateFrameOfReferenceUid()
    {
        return Generate();
    }

    /// <summary>
    /// Generates a deterministic UID based on input data (for testing/reproducibility)
    /// </summary>
    public static string GenerateDeterministic(string input)
    {
        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));

        // Convert hash to numeric string
        var numericHash = new StringBuilder();
        foreach (var b in hashBytes)
        {
            numericHash.Append((b % 100).ToString("D2"));
        }

        var uid = $"{OrgRoot}.{numericHash.ToString().Substring(0, 20)}";

        // Ensure UID doesn't exceed 64 characters
        if (uid.Length > 64)
        {
            uid = uid.Substring(0, 64);
        }

        return uid;
    }

    /// <summary>
    /// Validates a DICOM UID format
    /// </summary>
    public static bool IsValidUid(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
            return false;

        if (uid.Length > 64)
            return false;

        // UID should only contain digits and periods
        if (!uid.All(c => char.IsDigit(c) || c == '.'))
            return false;

        // Should not start or end with a period
        if (uid.StartsWith('.') || uid.EndsWith('.'))
            return false;

        // Should not have consecutive periods
        if (uid.Contains(".."))
            return false;

        return true;
    }
}
