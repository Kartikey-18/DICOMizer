using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DICOMizer.Models;
using DICOMizer.Services;
using DICOMizer.Utilities;
using Microsoft.Win32;

namespace DICOMizer.Views;

public partial class MainWindow : Window
{
    private readonly VideoProcessingService _videoService;
    private readonly DicomConversionService _dicomService;
    private readonly PacsService _pacsService;
    private readonly SettingsService _settingsService;

    private VideoMetadata? _currentVideoMetadata;
    private ConversionJob? _currentJob;
    private AppSettings? _settings;

    public MainWindow()
    {
        InitializeComponent();

        _videoService = new VideoProcessingService();
        _dicomService = new DicomConversionService();
        _pacsService = new PacsService();
        _settingsService = new SettingsService();

        Loaded += MainWindow_Loaded;
        StudyDatePicker.SelectedDate = DateTime.Now;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Constants.EnsureDirectoriesExist();
            _settings = await _settingsService.LoadSettingsAsync();

            // Check FFmpeg availability
            if (!_videoService.CheckFFmpegAvailable())
            {
                MessageBox.Show(
                    "FFmpeg not found. Please ensure FFmpeg is installed in the Resources/FFmpeg folder.",
                    "Missing Dependency",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Initialization error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DropZone_Click(object sender, MouseButtonEventArgs e)
    {
        BrowseForVideo();
    }

    private void DropZone_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            DropZoneBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007ACC"));
            DropZoneBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EBF5FF"));
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e)
    {
        DropZoneBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B0C4DE"));
        DropZoneBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        DropZoneBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B0C4DE"));
        DropZoneBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0)
            {
                var ext = Path.GetExtension(files[0]).ToLowerInvariant();
                var validExtensions = new[] { ".mp4", ".avi", ".mov", ".mkv", ".wmv", ".flv", ".m4v" };
                if (validExtensions.Contains(ext))
                {
                    await LoadVideoAsync(files[0]);
                }
                else
                {
                    MessageBox.Show("Unsupported file format. Please drop a video file.", "Invalid File", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }

    private void RemoveVideoButton_Click(object sender, RoutedEventArgs e)
    {
        _currentVideoMetadata = null;
        VideoPathTextBox.Text = "";
        VideoInfoTextBlock.Text = "";
        VideoPreview.Source = null;
        TrimButton.IsEnabled = false;

        DropZoneBorder.Visibility = Visibility.Visible;
        VideoInfoPanel.Visibility = Visibility.Collapsed;

        StatusTextBlock.Text = "Ready";
        ProgressBar.Value = 0;
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        BrowseForVideo();
    }

    private async void BrowseForVideo()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Video File",
            Filter = "Video Files|*.mp4;*.avi;*.mov;*.mkv;*.wmv;*.flv;*.m4v|All Files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            await LoadVideoAsync(dialog.FileName);
        }
    }

    private async Task LoadVideoAsync(string filePath)
    {
        try
        {
            StatusTextBlock.Text = "Analyzing video...";
            ProgressBar.IsIndeterminate = true;

            _currentVideoMetadata = await _videoService.AnalyzeVideoAsync(filePath);

            if (_currentVideoMetadata != null)
            {
                VideoPathTextBox.Text = _currentVideoMetadata.FilePath;
                VideoFileNameText.Text = Path.GetFileName(_currentVideoMetadata.FilePath);
                VideoInfoTextBlock.Text = $"{_currentVideoMetadata.GetResolutionString()}  ·  " +
                                         $"{_currentVideoMetadata.Duration:hh\\:mm\\:ss}  ·  " +
                                         $"{_currentVideoMetadata.FrameRate:F2} fps  ·  " +
                                         $"{_currentVideoMetadata.GetFileSizeString()}";

                VideoPreview.Source = new Uri(_currentVideoMetadata.FilePath);
                VideoPreview.Play();
                VideoPreview.Pause();

                TrimButton.IsEnabled = true;

                DropZoneBorder.Visibility = Visibility.Collapsed;
                VideoInfoPanel.Visibility = Visibility.Visible;

                StatusTextBlock.Text = "Video loaded successfully";
            }

            ProgressBar.IsIndeterminate = false;
            ProgressBar.Value = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load video: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusTextBlock.Text = "Failed to load video";
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Value = 0;
        }
    }

    private void TrimButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentVideoMetadata == null)
            return;

        var trimWindow = new TrimWindow(_currentVideoMetadata, _videoService);
        if (trimWindow.ShowDialog() == true)
        {
            _currentVideoMetadata = trimWindow.UpdatedMetadata;
            VideoInfoTextBlock.Text = $"{_currentVideoMetadata.GetResolutionString()}  ·  " +
                                     $"{_currentVideoMetadata.EffectiveDuration:hh\\:mm\\:ss} (Trimmed)  ·  " +
                                     $"{_currentVideoMetadata.FrameRate:F2} fps  ·  " +
                                     $"{_currentVideoMetadata.GetFileSizeString()}";
        }
    }

    private async void ConvertButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentVideoMetadata == null)
        {
            MessageBox.Show("Please select a video file first.", "No Video", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var patientMetadata = GetPatientMetadataFromForm();
        if (!patientMetadata.IsValid())
        {
            var errors = string.Join("\n", patientMetadata.Validate().Select(v => v.ErrorMessage));
            MessageBox.Show($"Please fix the following errors:\n\n{errors}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!SaveToFileCheckBox.IsChecked!.Value && !SendToPacsCheckBox.IsChecked!.Value)
        {
            MessageBox.Show("Please select at least one output option (Save to file or Send to PACS).", "No Output", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _currentJob = new ConversionJob
            {
                VideoMetadata = _currentVideoMetadata,
                PatientMetadata = patientMetadata,
                SaveToFile = SaveToFileCheckBox.IsChecked.Value,
                SendToPacs = SendToPacsCheckBox.IsChecked.Value,
                CancellationTokenSource = new CancellationTokenSource()
            };

            DisableControls();
            CancelButton.IsEnabled = true;

            await PerformConversionAsync(_currentJob);
        }
        catch (OperationCanceledException)
        {
            StatusTextBlock.Text = "Conversion cancelled";
            MessageBox.Show("Conversion was cancelled.", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Error: {ex.Message}";
            MessageBox.Show($"Conversion failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            EnableControls();
            CancelButton.IsEnabled = false;
            _currentJob?.Cleanup();
        }
    }

    private async Task PerformConversionAsync(ConversionJob job)
    {
        var progress = new Progress<int>(value =>
        {
            ProgressBar.Value = value;
        });

        job.MarkAsStarted();
        UpdateStatus("Processing video...", 0);

        // Trim if needed
        string videoPath = job.VideoMetadata.FilePath;
        if (job.VideoMetadata.IsTrimmed)
        {
            UpdateStatus("Trimming video...", 0);
            videoPath = await _videoService.TrimVideoAsync(
                job.VideoMetadata.FilePath,
                job.VideoMetadata.TrimStart ?? TimeSpan.Zero,
                job.VideoMetadata.TrimEnd ?? job.VideoMetadata.Duration,
                progress,
                job.CancellationTokenSource!.Token);
            job.TempVideoPath = videoPath;
        }

        // Transcode to H.264
        UpdateStatus("Transcoding to H.264...", 0);
        var h264Path = await _videoService.TranscodeToH264Async(
            videoPath,
            job.VideoMetadata,
            progress,
            job.CancellationTokenSource!.Token);
        job.ProcessedVideoPath = h264Path;

        // Create DICOM
        UpdateStatus("Creating DICOM file...", 0);
        var dicomPath = await _dicomService.CreateDicomFromVideoAsync(
            h264Path,
            job.VideoMetadata,
            job.PatientMetadata,
            progress,
            job.CancellationTokenSource!.Token);
        job.OutputFilePath = dicomPath;

        // Send to PACS if requested
        if (job.SendToPacs)
        {
            UpdateStatus("Sending to PACS...", 0);
            var pacsConfig = await _settingsService.GetPacsConfigurationAsync();
            await _pacsService.SendToPacsAsync(
                dicomPath,
                pacsConfig,
                progress,
                job.CancellationTokenSource!.Token);
        }

        job.MarkAsCompleted(dicomPath);
        UpdateStatus("Conversion completed successfully!", 100);

        MessageBox.Show(
            $"DICOM file created successfully!\n\nSaved to: {dicomPath}",
            "Success",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        if (_settings?.AutoOpenOutputFolder == true)
        {
            PathHelper.OpenFileInExplorer(dicomPath);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _currentJob?.CancellationTokenSource?.Cancel();
        CancelButton.IsEnabled = false;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_settingsService);
        settingsWindow.ShowDialog();
    }

    private void OpenOutputButton_Click(object sender, RoutedEventArgs e)
    {
        PathHelper.OpenFolderInExplorer(Constants.DefaultOutputPath);
    }

    private PatientMetadata GetPatientMetadataFromForm()
    {
        return new PatientMetadata
        {
            PatientId = PatientIdTextBox.Text,
            PatientName = PatientNameTextBox.Text,
            PatientBirthDate = PatientBirthDatePicker.SelectedDate?.ToString("yyyyMMdd"),
            PatientSex = (PatientSexComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString(),
            StudyDescription = StudyDescriptionTextBox.Text,
            PerformingPhysicianName = PhysicianNameTextBox.Text,
            Modality = (ModalityComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ES",
            StudyDate = GetStudyDateTime(),
            SeriesDate = GetStudyDateTime(),
            ContentDate = GetStudyDateTime()
        };
    }

    private DateTime GetStudyDateTime()
    {
        var date = StudyDatePicker.SelectedDate ?? DateTime.Now;
        if (TimeSpan.TryParse(StudyTimeTextBox.Text, out var time))
            return date.Date + time;
        return date;
    }

    private void UpdateStatus(string message, int progress)
    {
        StatusTextBlock.Text = message;
        ProgressBar.Value = progress;
    }

    private void DisableControls()
    {
        BrowseButton.IsEnabled = false;
        TrimButton.IsEnabled = false;
        ConvertButton.IsEnabled = false;
        SettingsButton.IsEnabled = false;
        PatientIdTextBox.IsEnabled = false;
        PatientNameTextBox.IsEnabled = false;
        PatientBirthDatePicker.IsEnabled = false;
        PatientSexComboBox.IsEnabled = false;
        StudyDescriptionTextBox.IsEnabled = false;
        StudyDatePicker.IsEnabled = false;
        StudyTimeTextBox.IsEnabled = false;
        PhysicianNameTextBox.IsEnabled = false;
        ModalityComboBox.IsEnabled = false;
        SaveToFileCheckBox.IsEnabled = false;
        SendToPacsCheckBox.IsEnabled = false;
    }

    private void EnableControls()
    {
        BrowseButton.IsEnabled = true;
        TrimButton.IsEnabled = _currentVideoMetadata != null;
        ConvertButton.IsEnabled = true;
        SettingsButton.IsEnabled = true;
        PatientIdTextBox.IsEnabled = true;
        PatientNameTextBox.IsEnabled = true;
        PatientBirthDatePicker.IsEnabled = true;
        PatientSexComboBox.IsEnabled = true;
        StudyDescriptionTextBox.IsEnabled = true;
        StudyDatePicker.IsEnabled = true;
        StudyTimeTextBox.IsEnabled = true;
        PhysicianNameTextBox.IsEnabled = true;
        ModalityComboBox.IsEnabled = true;
        SaveToFileCheckBox.IsEnabled = true;
        SendToPacsCheckBox.IsEnabled = true;
    }
}
