using System.Windows;
using DICOMizer.Models;
using DICOMizer.Services;

namespace DICOMizer.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly PacsService _pacsService;
    private AppSettings? _settings;

    public SettingsWindow(SettingsService settingsService)
    {
        InitializeComponent();

        _settingsService = settingsService;
        _pacsService = new PacsService();

        Loaded += SettingsWindow_Loaded;
    }

    private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings = await _settingsService.LoadSettingsAsync();
            LoadSettingsToForm();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadSettingsToForm()
    {
        if (_settings == null)
            return;

        var pacsConfig = _settings.PacsConfiguration;
        HostTextBox.Text = pacsConfig.Host;
        PortTextBox.Text = pacsConfig.Port.ToString();
        AeTitleTextBox.Text = pacsConfig.AeTitle;
        CalledAeTitleTextBox.Text = pacsConfig.CalledAeTitle;
        TimeoutTextBox.Text = pacsConfig.TimeoutSeconds.ToString();
        UseTlsCheckBox.IsChecked = pacsConfig.UseTls;

        AutoOpenFolderCheckBox.IsChecked = _settings.AutoOpenOutputFolder;
        HardwareAccelerationCheckBox.IsChecked = _settings.EnableHardwareAcceleration;
    }

    private PacsConfiguration GetPacsConfigFromForm()
    {
        return new PacsConfiguration
        {
            Host = HostTextBox.Text,
            Port = int.TryParse(PortTextBox.Text, out var port) ? port : 104,
            AeTitle = AeTitleTextBox.Text,
            CalledAeTitle = CalledAeTitleTextBox.Text,
            TimeoutSeconds = int.TryParse(TimeoutTextBox.Text, out var timeout) ? timeout : 30,
            UseTls = UseTlsCheckBox.IsChecked ?? false
        };
    }

    private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        var pacsConfig = GetPacsConfigFromForm();

        if (!pacsConfig.IsValid())
        {
            var errors = string.Join("\n", pacsConfig.Validate().Select(v => v.ErrorMessage));
            MessageBox.Show($"Please fix the following errors:\n\n{errors}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            TestConnectionButton.IsEnabled = false;
            ConnectionStatusTextBlock.Text = "Testing connection...";

            var status = await _pacsService.GetConnectionStatusAsync(pacsConfig);

            if (status.IsConnected)
            {
                ConnectionStatusTextBlock.Text = $"✓ Connected successfully! (Response: {status.ResponseTime.TotalMilliseconds:F0} ms)";
                ConnectionStatusTextBlock.Foreground = System.Windows.Media.Brushes.Green;
                MessageBox.Show(
                    "PACS connection test successful!",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                ConnectionStatusTextBlock.Text = $"✗ Connection failed: {status.ErrorMessage}";
                ConnectionStatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                MessageBox.Show(
                    $"PACS connection test failed:\n\n{status.ErrorMessage}",
                    "Connection Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            ConnectionStatusTextBlock.Text = $"✗ Error: {ex.Message}";
            ConnectionStatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
            MessageBox.Show($"Connection test error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            TestConnectionButton.IsEnabled = true;
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var pacsConfig = GetPacsConfigFromForm();

        if (!pacsConfig.IsValid())
        {
            var errors = string.Join("\n", pacsConfig.Validate().Select(v => v.ErrorMessage));
            MessageBox.Show($"Please fix the following errors:\n\n{errors}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (_settings == null)
                _settings = new AppSettings();

            _settings.PacsConfiguration = pacsConfig;
            _settings.AutoOpenOutputFolder = AutoOpenFolderCheckBox.IsChecked ?? true;
            _settings.EnableHardwareAcceleration = HardwareAccelerationCheckBox.IsChecked ?? true;

            await _settingsService.SaveSettingsAsync(_settings);

            MessageBox.Show("Settings saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
