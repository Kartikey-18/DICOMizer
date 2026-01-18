using System.Windows;
using System.Windows.Threading;
using DICOMizer.Models;
using DICOMizer.Services;

namespace DICOMizer.Views;

public partial class TrimWindow : Window
{
    private readonly VideoMetadata _originalMetadata;
    private readonly VideoProcessingService _videoService;
    private readonly DispatcherTimer _timer;

    private TimeSpan _startTime;
    private TimeSpan _endTime;
    private bool _isPlaying;
    private bool _isUpdatingSlider;

    public VideoMetadata UpdatedMetadata { get; private set; }

    public TrimWindow(VideoMetadata metadata, VideoProcessingService videoService)
    {
        InitializeComponent();

        _originalMetadata = metadata;
        UpdatedMetadata = metadata;
        _videoService = videoService;

        _startTime = TimeSpan.Zero;
        _endTime = metadata.Duration;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _timer.Tick += Timer_Tick;

        Loaded += TrimWindow_Loaded;
        Closing += TrimWindow_Closing;
    }

    private void TrimWindow_Loaded(object sender, RoutedEventArgs e)
    {
        VideoInfoTextBlock.Text = $"{_originalMetadata.FileName} - " +
                                  $"{_originalMetadata.GetResolutionString()} - " +
                                  $"{_originalMetadata.Duration:hh\\:mm\\:ss}";

        VideoPlayer.Source = new Uri(_originalMetadata.FilePath);
        VideoPlayer.Play();
        VideoPlayer.Pause();

        EndTimeTextBox.Text = FormatTimeSpan(_endTime);
        UpdateTrimmedDuration();
    }

    private void TrimWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _timer.Stop();
        VideoPlayer.Stop();
        VideoPlayer.Close();
    }

    private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (VideoPlayer.NaturalDuration.HasTimeSpan)
        {
            TimelineSlider.Maximum = VideoPlayer.NaturalDuration.TimeSpan.TotalSeconds;
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (VideoPlayer.Source != null && VideoPlayer.NaturalDuration.HasTimeSpan && !_isUpdatingSlider)
        {
            var currentPosition = VideoPlayer.Position;
            TimelineSlider.Value = currentPosition.TotalSeconds;
            UpdateTimeDisplay();

            // Auto-pause at end time
            if (_isPlaying && currentPosition >= _endTime)
            {
                VideoPlayer.Pause();
                _isPlaying = false;
            }
        }
    }

    private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isUpdatingSlider)
        {
            var newPosition = TimeSpan.FromSeconds(TimelineSlider.Value);
            VideoPlayer.Position = newPosition;
            UpdateTimeDisplay();
        }
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        VideoPlayer.Play();
        _isPlaying = true;
        _timer.Start();
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        VideoPlayer.Pause();
        _isPlaying = false;
        _timer.Stop();
    }

    private void SetStartButton_Click(object sender, RoutedEventArgs e)
    {
        _startTime = VideoPlayer.Position;
        StartTimeTextBox.Text = FormatTimeSpan(_startTime);
        UpdateTrimmedDuration();
    }

    private void SetEndButton_Click(object sender, RoutedEventArgs e)
    {
        _endTime = VideoPlayer.Position;
        EndTimeTextBox.Text = FormatTimeSpan(_endTime);
        UpdateTrimmedDuration();
    }

    private void JumpToStartButton_Click(object sender, RoutedEventArgs e)
    {
        if (TryParseTimeSpan(StartTimeTextBox.Text, out var time))
        {
            _startTime = time;
            VideoPlayer.Position = _startTime;
            UpdateTimeDisplay();
        }
    }

    private void JumpToEndButton_Click(object sender, RoutedEventArgs e)
    {
        if (TryParseTimeSpan(EndTimeTextBox.Text, out var time))
        {
            _endTime = time;
            VideoPlayer.Position = _endTime;
            UpdateTimeDisplay();
        }
    }

    private void PrevFrameButton_Click(object sender, RoutedEventArgs e)
    {
        var frameDuration = TimeSpan.FromSeconds(1.0 / _originalMetadata.FrameRate);
        var newPosition = VideoPlayer.Position - frameDuration;
        if (newPosition < TimeSpan.Zero)
            newPosition = TimeSpan.Zero;

        VideoPlayer.Position = newPosition;
        UpdateTimeDisplay();
    }

    private void NextFrameButton_Click(object sender, RoutedEventArgs e)
    {
        var frameDuration = TimeSpan.FromSeconds(1.0 / _originalMetadata.FrameRate);
        var newPosition = VideoPlayer.Position + frameDuration;
        if (newPosition > _originalMetadata.Duration)
            newPosition = _originalMetadata.Duration;

        VideoPlayer.Position = newPosition;
        UpdateTimeDisplay();
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        // Validate times
        if (!TryParseTimeSpan(StartTimeTextBox.Text, out _startTime))
        {
            MessageBox.Show("Invalid start time format. Use hh:mm:ss", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryParseTimeSpan(EndTimeTextBox.Text, out _endTime))
        {
            MessageBox.Show("Invalid end time format. Use hh:mm:ss", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_startTime >= _endTime)
        {
            MessageBox.Show("Start time must be before end time.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_endTime > _originalMetadata.Duration)
        {
            MessageBox.Show("End time exceeds video duration.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Create updated metadata
        var trimmedMetadata = new VideoMetadata
        {
            FilePath = _originalMetadata.FilePath,
            FileName = _originalMetadata.FileName,
            FileSizeBytes = _originalMetadata.FileSizeBytes,
            Duration = _originalMetadata.Duration,
            Width = _originalMetadata.Width,
            Height = _originalMetadata.Height,
            FrameRate = _originalMetadata.FrameRate,
            CodecName = _originalMetadata.CodecName,
            PixelFormat = _originalMetadata.PixelFormat,
            BitRate = _originalMetadata.BitRate,
            TrimStart = _startTime,
            TrimEnd = _endTime
        };

        UpdatedMetadata = trimmedMetadata;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void UpdateTimeDisplay()
    {
        var current = VideoPlayer.Position;
        var total = VideoPlayer.NaturalDuration.HasTimeSpan
            ? VideoPlayer.NaturalDuration.TimeSpan
            : _originalMetadata.Duration;

        TimeDisplayTextBlock.Text = $"{FormatTimeSpan(current)} / {FormatTimeSpan(total)}";
    }

    private void UpdateTrimmedDuration()
    {
        var duration = _endTime - _startTime;
        TrimmedDurationTextBlock.Text = FormatTimeSpan(duration);
    }

    private string FormatTimeSpan(TimeSpan time)
    {
        return $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
    }

    private bool TryParseTimeSpan(string input, out TimeSpan result)
    {
        result = TimeSpan.Zero;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        var parts = input.Split(':');
        if (parts.Length != 3)
            return false;

        if (!int.TryParse(parts[0], out var hours) ||
            !int.TryParse(parts[1], out var minutes) ||
            !int.TryParse(parts[2], out var seconds))
            return false;

        if (hours < 0 || minutes < 0 || minutes >= 60 || seconds < 0 || seconds >= 60)
            return false;

        result = new TimeSpan(hours, minutes, seconds);
        return true;
    }
}
