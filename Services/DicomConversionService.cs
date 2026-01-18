using System.IO;
using DICOMizer.Models;
using DICOMizer.Utilities;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;

namespace DICOMizer.Services;

/// <summary>
/// Service for converting video files to DICOM format
/// </summary>
public class DicomConversionService
{
    /// <summary>
    /// Creates a DICOM file from an H.264 video file
    /// </summary>
    public async Task<string> CreateDicomFromVideoAsync(
        string videoPath,
        VideoMetadata videoMetadata,
        PatientMetadata patientMetadata,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(videoPath))
            throw new FileNotFoundException("Video file not found", videoPath);

        // Validate patient metadata
        if (!patientMetadata.IsValid())
        {
            var errors = string.Join(", ", patientMetadata.Validate().Select(v => v.ErrorMessage));
            throw new InvalidOperationException($"Invalid patient metadata: {errors}");
        }

        progress?.Report(0);

        // Read video data
        var videoData = await File.ReadAllBytesAsync(videoPath, cancellationToken);
        progress?.Report(30);

        // Create DICOM dataset
        var dicomFile = CreateDicomDataset(videoMetadata, patientMetadata);
        progress?.Report(50);

        // Add video data as encapsulated pixel data
        AddVideoPixelData(dicomFile, videoData);
        progress?.Report(80);

        // Generate output path
        var outputPath = PathHelper.GetOutputFilePath(
            patientMetadata.PatientId,
            patientMetadata.PatientName,
            patientMetadata.StudyDate);

        // Save DICOM file
        await dicomFile.SaveAsync(outputPath);
        progress?.Report(100);

        return outputPath;
    }

    /// <summary>
    /// Creates a DICOM dataset with required tags
    /// </summary>
    private DicomFile CreateDicomDataset(VideoMetadata videoMetadata, PatientMetadata patientMetadata)
    {
        var dataset = new DicomDataset();

        // SOP Common Module
        dataset.Add(DicomTag.SOPClassUID, Constants.VideoEndoscopicSopClassUid);
        dataset.Add(DicomTag.SOPInstanceUID, UidGenerator.GenerateSopInstanceUid());

        // Patient Module
        dataset.Add(DicomTag.PatientName, patientMetadata.PatientName);
        dataset.Add(DicomTag.PatientID, patientMetadata.PatientId);

        if (!string.IsNullOrWhiteSpace(patientMetadata.PatientBirthDate))
        {
            var birthDate = patientMetadata.GetDicomBirthDate();
            if (!string.IsNullOrEmpty(birthDate))
                dataset.Add(DicomTag.PatientBirthDate, birthDate);
        }

        if (!string.IsNullOrWhiteSpace(patientMetadata.PatientSex))
            dataset.Add(DicomTag.PatientSex, patientMetadata.PatientSex);

        // Study Module
        dataset.Add(DicomTag.StudyInstanceUID, UidGenerator.GenerateStudyUid());
        dataset.Add(DicomTag.StudyDate, PatientMetadata.FormatDicomDate(patientMetadata.StudyDate));
        dataset.Add(DicomTag.StudyTime, PatientMetadata.FormatDicomTime(patientMetadata.StudyDate));

        if (!string.IsNullOrWhiteSpace(patientMetadata.StudyDescription))
            dataset.Add(DicomTag.StudyDescription, patientMetadata.StudyDescription);

        // Series Module
        dataset.Add(DicomTag.SeriesInstanceUID, UidGenerator.GenerateSeriesUid());
        dataset.Add(DicomTag.SeriesDate, PatientMetadata.FormatDicomDate(patientMetadata.SeriesDate));
        dataset.Add(DicomTag.SeriesTime, PatientMetadata.FormatDicomTime(patientMetadata.SeriesDate));
        dataset.Add(DicomTag.Modality, "ES"); // Endoscopy

        if (!string.IsNullOrWhiteSpace(patientMetadata.SeriesDescription))
            dataset.Add(DicomTag.SeriesDescription, patientMetadata.SeriesDescription);

        dataset.Add(DicomTag.SeriesNumber, 1);

        // Equipment Module
        dataset.Add(DicomTag.Manufacturer, Constants.Manufacturer);
        dataset.Add(DicomTag.ManufacturerModelName, Constants.AppName);
        dataset.Add(DicomTag.SoftwareVersions, Constants.AppVersion);

        // Frame of Reference Module
        dataset.Add(DicomTag.FrameOfReferenceUID, UidGenerator.GenerateFrameOfReferenceUid());

        // General Image Module
        dataset.Add(DicomTag.InstanceNumber, 1);
        dataset.Add(DicomTag.ContentDate, PatientMetadata.FormatDicomDate(patientMetadata.ContentDate));
        dataset.Add(DicomTag.ContentTime, PatientMetadata.FormatDicomTime(patientMetadata.ContentDate));

        // Image Pixel Module
        dataset.Add(DicomTag.SamplesPerPixel, (ushort)3); // RGB
        dataset.Add(DicomTag.PhotometricInterpretation, "YBR_PARTIAL_420");
        dataset.Add(DicomTag.Rows, (ushort)videoMetadata.Height);
        dataset.Add(DicomTag.Columns, (ushort)videoMetadata.Width);
        dataset.Add(DicomTag.BitsAllocated, (ushort)8);
        dataset.Add(DicomTag.BitsStored, (ushort)8);
        dataset.Add(DicomTag.HighBit, (ushort)7);
        dataset.Add(DicomTag.PixelRepresentation, (ushort)0);

        // Cine Module
        dataset.Add(DicomTag.FrameTime, (decimal)(1000.0 / Constants.DefaultFrameRate)); // milliseconds per frame
        dataset.Add(DicomTag.CineRate, (decimal)Constants.DefaultFrameRate);
        dataset.Add(DicomTag.NumberOfFrames, CalculateNumberOfFrames(videoMetadata.EffectiveDuration));

        // Performing Physician
        if (!string.IsNullOrWhiteSpace(patientMetadata.PerformingPhysicianName))
            dataset.Add(DicomTag.PerformingPhysicianName, patientMetadata.PerformingPhysicianName);

        return new DicomFile(dataset);
    }

    /// <summary>
    /// Adds H.264 video data to DICOM file as encapsulated pixel data
    /// </summary>
    private void AddVideoPixelData(DicomFile dicomFile, byte[] videoData)
    {
        var dataset = dicomFile.Dataset;

        // Set transfer syntax to MPEG-4 AVC/H.264
        dicomFile.FileMetaInfo.TransferSyntax = DicomTransferSyntax.Lookup(DicomUID.Parse(Constants.Mpeg4TransferSyntaxUid));

        // Fragment the video data into 256KB chunks
        var fragments = FragmentVideoData(videoData);

        // Create encapsulated pixel data sequence
        var pixelDataSequence = new DicomOtherByteFragment(DicomTag.PixelData);

        // Add offset table (empty for video)
        pixelDataSequence.Fragments.Add(new MemoryByteBuffer(Array.Empty<byte>()));

        // Add fragments
        foreach (var fragment in fragments)
        {
            pixelDataSequence.Fragments.Add(new MemoryByteBuffer(fragment));
        }

        dataset.AddOrUpdate(pixelDataSequence);
    }

    /// <summary>
    /// Fragments video data into chunks for DICOM encapsulation
    /// </summary>
    private List<byte[]> FragmentVideoData(byte[] videoData)
    {
        var fragments = new List<byte[]>();
        var offset = 0;
        var fragmentSize = Constants.DicomFragmentSize;

        while (offset < videoData.Length)
        {
            var remainingBytes = videoData.Length - offset;
            var currentFragmentSize = Math.Min(fragmentSize, remainingBytes);

            var fragment = new byte[currentFragmentSize];
            Array.Copy(videoData, offset, fragment, 0, currentFragmentSize);

            fragments.Add(fragment);
            offset += currentFragmentSize;
        }

        return fragments;
    }

    /// <summary>
    /// Calculates the number of frames based on duration and frame rate
    /// </summary>
    private int CalculateNumberOfFrames(TimeSpan duration)
    {
        return (int)(duration.TotalSeconds * Constants.DefaultFrameRate);
    }

    /// <summary>
    /// Validates DICOM file after creation
    /// </summary>
    public async Task<bool> ValidateDicomFileAsync(string dicomPath)
    {
        try
        {
            if (!File.Exists(dicomPath))
                return false;

            var dicomFile = await DicomFile.OpenAsync(dicomPath);

            // Check required tags
            var requiredTags = new[]
            {
                DicomTag.SOPClassUID,
                DicomTag.SOPInstanceUID,
                DicomTag.PatientName,
                DicomTag.PatientID,
                DicomTag.StudyInstanceUID,
                DicomTag.SeriesInstanceUID
            };

            foreach (var tag in requiredTags)
            {
                if (!dicomFile.Dataset.Contains(tag))
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
