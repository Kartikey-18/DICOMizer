using System.IO;
using DICOMizer.Models;
using DICOMizer.Utilities;
using FellowOakDicom;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;

namespace DICOMizer.Services;

/// <summary>
/// Service for PACS communication via DICOM C-ECHO and C-STORE
/// </summary>
public class PacsService
{
    /// <summary>
    /// Tests PACS connection using C-ECHO
    /// </summary>
    public async Task<bool> TestConnectionAsync(
        PacsConfiguration config,
        CancellationToken cancellationToken = default)
    {
        if (!config.IsValid())
            throw new InvalidOperationException("Invalid PACS configuration");

        try
        {
            var client = DicomClientFactory.Create(
                config.Host,
                config.Port,
                config.UseTls,
                config.AeTitle,
                config.CalledAeTitle);

            client.NegotiateAsyncOps();

            var request = new DicomCEchoRequest();
            var echoCompleted = false;
            var echoSuccess = false;

            request.OnResponseReceived += (req, response) =>
            {
                echoSuccess = response.Status == DicomStatus.Success;
                echoCompleted = true;
            };

            await client.AddRequestAsync(request);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await client.SendAsync(linkedCts.Token);

            // Wait for echo completion
            var maxWaitTime = DateTime.Now.AddSeconds(config.TimeoutSeconds);
            while (!echoCompleted && DateTime.Now < maxWaitTime)
            {
                await Task.Delay(100, cancellationToken);
            }

            return echoSuccess;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"PACS connection test failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Sends a DICOM file to PACS using C-STORE
    /// </summary>
    public async Task<bool> SendToPacsAsync(
        string dicomFilePath,
        PacsConfiguration config,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(dicomFilePath))
            throw new FileNotFoundException("DICOM file not found", dicomFilePath);

        if (!config.IsValid())
            throw new InvalidOperationException("Invalid PACS configuration");

        try
        {
            progress?.Report(0);

            var dicomFile = await DicomFile.OpenAsync(dicomFilePath);
            progress?.Report(20);

            var client = DicomClientFactory.Create(
                config.Host,
                config.Port,
                config.UseTls,
                config.AeTitle,
                config.CalledAeTitle);

            client.NegotiateAsyncOps();

            var storeSuccess = false;
            var storeCompleted = false;
            string? errorMessage = null;

            var request = new DicomCStoreRequest(dicomFile);

            request.OnResponseReceived += (req, response) =>
            {
                if (response.Status == DicomStatus.Success)
                {
                    storeSuccess = true;
                }
                else
                {
                    errorMessage = $"C-STORE failed with status: {response.Status}";
                }
                storeCompleted = true;
                progress?.Report(80);
            };

            await client.AddRequestAsync(request);
            progress?.Report(40);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await client.SendAsync(linkedCts.Token);
            progress?.Report(60);

            // Wait for store completion
            var maxWaitTime = DateTime.Now.AddSeconds(config.TimeoutSeconds);
            while (!storeCompleted && DateTime.Now < maxWaitTime)
            {
                await Task.Delay(100, cancellationToken);
            }

            if (!storeCompleted)
                throw new TimeoutException("PACS transmission timed out");

            if (!storeSuccess)
                throw new InvalidOperationException(errorMessage ?? "C-STORE failed");

            progress?.Report(100);
            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"PACS transmission failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Sends multiple DICOM files to PACS
    /// </summary>
    public async Task<bool> SendMultipleToPacsAsync(
        IEnumerable<string> dicomFilePaths,
        PacsConfiguration config,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var files = dicomFilePaths.ToList();
        if (!files.Any())
            return false;

        var totalFiles = files.Count;
        var processedFiles = 0;

        foreach (var filePath in files)
        {
                await SendToPacsAsync(filePath, config, null, cancellationToken);

                processedFiles++;
                var overallProgress = (int)((processedFiles / (double)totalFiles) * 100);
                progress?.Report(overallProgress);
            }

        return true;
    }

    /// <summary>
    /// Validates PACS configuration before transmission
    /// </summary>
    public bool ValidateConfiguration(PacsConfiguration config)
    {
        return config.IsValid();
    }

    /// <summary>
    /// Gets PACS connection status information
    /// </summary>
    public async Task<PacsConnectionStatus> GetConnectionStatusAsync(
        PacsConfiguration config,
        CancellationToken cancellationToken = default)
    {
        var status = new PacsConnectionStatus
        {
            Configuration = config,
            TestStartTime = DateTime.Now
        };

        try
        {
            var success = await TestConnectionAsync(config, cancellationToken);
            status.IsConnected = success;
            status.TestEndTime = DateTime.Now;
            status.ResponseTime = status.TestEndTime - status.TestStartTime;
        }
        catch (Exception ex)
        {
            status.IsConnected = false;
            status.ErrorMessage = ex.Message;
            status.TestEndTime = DateTime.Now;
        }

        return status;
    }
}

/// <summary>
/// Represents PACS connection status
/// </summary>
public class PacsConnectionStatus
{
    public PacsConfiguration Configuration { get; set; } = new();
    public bool IsConnected { get; set; }
    public DateTime TestStartTime { get; set; }
    public DateTime TestEndTime { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public string? ErrorMessage { get; set; }

    public string GetStatusMessage()
    {
        if (IsConnected)
            return $"Connected successfully (Response time: {ResponseTime.TotalMilliseconds:F0} ms)";

        return $"Connection failed: {ErrorMessage}";
    }
}
