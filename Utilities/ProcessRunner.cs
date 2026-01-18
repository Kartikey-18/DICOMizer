using System.Diagnostics;
using System.Text;

namespace DICOMizer.Utilities;

/// <summary>
/// Utility for running external processes with output capture and progress tracking
/// </summary>
public class ProcessRunner
{
    public event EventHandler<string>? OutputReceived;
    public event EventHandler<string>? ErrorReceived;
    public event EventHandler<int>? ProgressChanged;

    /// <summary>
    /// Runs an external process and captures output
    /// </summary>
    public async Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken = default,
        IProgress<string>? progress = null)
    {
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                outputBuilder.AppendLine(e.Data);
                OutputReceived?.Invoke(this, e.Data);
                progress?.Report(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                errorBuilder.AppendLine(e.Data);
                ErrorReceived?.Invoke(this, e.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for process to exit or cancellation
            await Task.Run(() =>
            {
                while (!process.HasExited)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            process.Kill(true);
                        }
                        catch
                        {
                            // Ignore errors when killing process
                        }
                        throw new OperationCanceledException();
                    }
                    Thread.Sleep(100);
                }
            }, cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            return new ProcessResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = outputBuilder.ToString(),
                StandardError = errorBuilder.ToString(),
                Success = process.ExitCode == 0
            };
        }
        catch (OperationCanceledException)
        {
            return new ProcessResult
            {
                ExitCode = -1,
                StandardOutput = outputBuilder.ToString(),
                StandardError = "Process was cancelled",
                Success = false
            };
        }
        catch (Exception ex)
        {
            return new ProcessResult
            {
                ExitCode = -1,
                StandardOutput = outputBuilder.ToString(),
                StandardError = $"Process execution failed: {ex.Message}",
                Success = false
            };
        }
    }

    /// <summary>
    /// Runs FFmpeg with progress tracking
    /// </summary>
    public async Task<ProcessResult> RunFFmpegAsync(
        string arguments,
        TimeSpan? duration = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        var startInfo = new ProcessStartInfo
        {
            FileName = Constants.FFmpegPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                errorBuilder.AppendLine(e.Data);

                // Parse FFmpeg progress from stderr
                if (duration.HasValue && progress != null)
                {
                    var timeMatch = System.Text.RegularExpressions.Regex.Match(e.Data, @"time=(\d+):(\d+):(\d+\.\d+)");
                    if (timeMatch.Success)
                    {
                        var hours = int.Parse(timeMatch.Groups[1].Value);
                        var minutes = int.Parse(timeMatch.Groups[2].Value);
                        var seconds = double.Parse(timeMatch.Groups[3].Value);

                        var currentTime = TimeSpan.FromHours(hours) +
                                        TimeSpan.FromMinutes(minutes) +
                                        TimeSpan.FromSeconds(seconds);

                        var percentage = (int)((currentTime.TotalSeconds / duration.Value.TotalSeconds) * 100);
                        percentage = Math.Clamp(percentage, 0, 100);
                        progress.Report(percentage);
                        ProgressChanged?.Invoke(this, percentage);
                    }
                }
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await Task.Run(() =>
            {
                while (!process.HasExited)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            process.Kill(true);
                        }
                        catch { }
                        throw new OperationCanceledException();
                    }
                    Thread.Sleep(100);
                }
            }, cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            return new ProcessResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = outputBuilder.ToString(),
                StandardError = errorBuilder.ToString(),
                Success = process.ExitCode == 0
            };
        }
        catch (OperationCanceledException)
        {
            return new ProcessResult
            {
                ExitCode = -1,
                StandardOutput = outputBuilder.ToString(),
                StandardError = "FFmpeg process was cancelled",
                Success = false
            };
        }
        catch (Exception ex)
        {
            return new ProcessResult
            {
                ExitCode = -1,
                StandardOutput = outputBuilder.ToString(),
                StandardError = $"FFmpeg execution failed: {ex.Message}",
                Success = false
            };
        }
    }
}

/// <summary>
/// Represents the result of a process execution
/// </summary>
public class ProcessResult
{
    public int ExitCode { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public bool Success { get; set; }

    public string GetOutput() => StandardOutput + StandardError;
}
