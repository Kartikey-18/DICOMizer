using DICOMizer.Utilities;
using System.IO;

namespace DICOMizer.Services;

/// <summary>
/// Service for application logging
/// </summary>
public class LoggingService
{
    private static readonly object LockObject = new();
    private static LoggingService? _instance;
    private readonly string _logDirectory;
    private string _currentLogFile;

    private LoggingService()
    {
        _logDirectory = Constants.LogsPath;
        Directory.CreateDirectory(_logDirectory);
        _currentLogFile = GetCurrentLogFilePath();

        // Clean up old logs
        CleanupOldLogs();
    }

    public static LoggingService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (LockObject)
                {
                    _instance ??= new LoggingService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Logs an informational message
    /// </summary>
    public void LogInfo(string message, string? category = null)
    {
        Log(LogLevel.Info, message, category);
    }

    /// <summary>
    /// Logs a warning message
    /// </summary>
    public void LogWarning(string message, string? category = null)
    {
        Log(LogLevel.Warning, message, category);
    }

    /// <summary>
    /// Logs an error message
    /// </summary>
    public void LogError(string message, Exception? exception = null, string? category = null)
    {
        var fullMessage = exception != null
            ? $"{message}\nException: {exception.GetType().Name}\nMessage: {exception.Message}\nStackTrace: {exception.StackTrace}"
            : message;

        Log(LogLevel.Error, fullMessage, category);
    }

    /// <summary>
    /// Logs a debug message
    /// </summary>
    public void LogDebug(string message, string? category = null)
    {
#if DEBUG
        Log(LogLevel.Debug, message, category);
#endif
    }

    /// <summary>
    /// Core logging method
    /// </summary>
    private void Log(LogLevel level, string message, string? category = null)
    {
        try
        {
            lock (LockObject)
            {
                // Check if we need to rotate log file
                CheckLogRotation();

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var categoryStr = string.IsNullOrWhiteSpace(category) ? "" : $"[{category}] ";
                var logEntry = $"{timestamp} [{level}] {categoryStr}{message}";

                File.AppendAllText(_currentLogFile, logEntry + Environment.NewLine);

                // Also output to debug console
                System.Diagnostics.Debug.WriteLine(logEntry);
            }
        }
        catch
        {
            // Silently fail if logging fails
        }
    }

    /// <summary>
    /// Checks if log file needs rotation
    /// </summary>
    private void CheckLogRotation()
    {
        var fileInfo = new FileInfo(_currentLogFile);

        // Rotate if file exceeds max size or if date changed
        if (fileInfo.Exists && fileInfo.Length > Constants.MaxLogFileSizeBytes)
        {
            var rotatedFile = Path.Combine(_logDirectory,
                $"dicomizer_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            File.Move(_currentLogFile, rotatedFile);
            _currentLogFile = GetCurrentLogFilePath();
        }
        else if (fileInfo.Exists)
        {
            var fileDate = fileInfo.CreationTime.Date;
            var currentDate = DateTime.Now.Date;
            if (fileDate != currentDate)
            {
                _currentLogFile = GetCurrentLogFilePath();
            }
        }
    }

    /// <summary>
    /// Gets the current log file path
    /// </summary>
    private string GetCurrentLogFilePath()
    {
        return Path.Combine(_logDirectory, $"dicomizer_{DateTime.Now:yyyyMMdd}.log");
    }

    /// <summary>
    /// Cleans up old log files based on retention policy
    /// </summary>
    private void CleanupOldLogs()
    {
        try
        {
            var files = Directory.GetFiles(_logDirectory, "dicomizer_*.log");
            var cutoffDate = DateTime.Now.AddDays(-Constants.LogRetentionDays);

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTime < cutoffDate)
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Opens the log directory in Windows Explorer
    /// </summary>
    public void OpenLogDirectory()
    {
        if (Directory.Exists(_logDirectory))
        {
            System.Diagnostics.Process.Start("explorer.exe", _logDirectory);
        }
    }

    /// <summary>
    /// Gets the current log file path
    /// </summary>
    public string GetCurrentLogFile() => _currentLogFile;
}

/// <summary>
/// Log levels
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}
