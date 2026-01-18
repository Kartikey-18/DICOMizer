using DICOMizer.Models;
using DICOMizer.Utilities;
using System.Text.Json;

namespace DICOMizer.Services;

/// <summary>
/// Service for managing application settings persistence
/// </summary>
public class SettingsService
{
    private readonly string _settingsPath;
    private AppSettings? _cachedSettings;

    public SettingsService()
    {
        _settingsPath = Constants.SettingsPath;
        Constants.EnsureDirectoriesExist();
    }

    /// <summary>
    /// Loads settings from disk
    /// </summary>
    public async Task<AppSettings> LoadSettingsAsync()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                var defaultSettings = CreateDefaultSettings();
                await SaveSettingsAsync(defaultSettings);
                return defaultSettings;
            }

            var json = await File.ReadAllTextAsync(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);

            _cachedSettings = settings ?? CreateDefaultSettings();
            return _cachedSettings;
        }
        catch (Exception ex)
        {
            // If loading fails, return default settings
            var defaultSettings = CreateDefaultSettings();
            _cachedSettings = defaultSettings;
            return defaultSettings;
        }
    }

    /// <summary>
    /// Saves settings to disk
    /// </summary>
    public async Task SaveSettingsAsync(AppSettings settings)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(settings, options);
            await File.WriteAllTextAsync(_settingsPath, json);

            _cachedSettings = settings;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save settings: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets cached settings or loads them if not cached
    /// </summary>
    public async Task<AppSettings> GetSettingsAsync()
    {
        if (_cachedSettings == null)
        {
            return await LoadSettingsAsync();
        }

        return _cachedSettings;
    }

    /// <summary>
    /// Updates PACS configuration
    /// </summary>
    public async Task UpdatePacsConfigurationAsync(PacsConfiguration config)
    {
        var settings = await GetSettingsAsync();
        settings.PacsConfiguration = config;
        await SaveSettingsAsync(settings);
    }

    /// <summary>
    /// Gets PACS configuration
    /// </summary>
    public async Task<PacsConfiguration> GetPacsConfigurationAsync()
    {
        var settings = await GetSettingsAsync();
        return settings.PacsConfiguration;
    }

    /// <summary>
    /// Resets settings to default
    /// </summary>
    public async Task ResetSettingsAsync()
    {
        var defaultSettings = CreateDefaultSettings();
        await SaveSettingsAsync(defaultSettings);
    }

    /// <summary>
    /// Creates default settings
    /// </summary>
    private AppSettings CreateDefaultSettings()
    {
        return new AppSettings
        {
            PacsConfiguration = new PacsConfiguration
            {
                Host = "localhost",
                Port = Constants.DefaultPacsPort,
                AeTitle = "DICOMIZER",
                CalledAeTitle = "EUNITY",
                TimeoutSeconds = Constants.DefaultPacsTimeout,
                UseTls = false
            },
            LastOutputDirectory = Constants.DefaultOutputPath,
            RememberLastPatient = false,
            AutoOpenOutputFolder = true,
            EnableHardwareAcceleration = true
        };
    }
}

/// <summary>
/// Application settings model
/// </summary>
public class AppSettings
{
    public PacsConfiguration PacsConfiguration { get; set; } = new();
    public string LastOutputDirectory { get; set; } = string.Empty;
    public bool RememberLastPatient { get; set; }
    public PatientMetadata? LastPatientMetadata { get; set; }
    public bool AutoOpenOutputFolder { get; set; }
    public bool EnableHardwareAcceleration { get; set; }
    public string AppVersion { get; set; } = Constants.AppVersion;
}
