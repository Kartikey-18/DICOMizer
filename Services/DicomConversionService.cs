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
    /// Format matches working eUnity-compatible DICOM video files
    /// </summary>
    private DicomFile CreateDicomDataset(VideoMetadata videoMetadata, PatientMetadata patientMetadata)
    {
        var dataset = new DicomDataset();

        // Image Type - matches working files
        dataset.Add(DicomTag.ImageType, "ORIGINAL", "SECONDARY");

        // SOP Common Module
        dataset.Add(DicomTag.SOPClassUID, Constants.VideoEndoscopicSopClassUid);
        dataset.Add(DicomTag.SOPInstanceUID, UidGenerator.GenerateSopInstanceUid());

        // General Study Module
        dataset.Add(DicomTag.StudyDate, PatientMetadata.FormatDicomDate(patientMetadata.StudyDate));
        dataset.Add(DicomTag.StudyTime, PatientMetadata.FormatDicomTime(patientMetadata.StudyDate));
        dataset.Add(DicomTag.AccessionNumber, patientMetadata.AccessionNumber ?? "");
        dataset.Add(DicomTag.Modality, "ES"); // Endoscopy
        dataset.Add(DicomTag.ConversionType, "DV"); // Digitized Video
        dataset.Add(DicomTag.Manufacturer, Constants.Manufacturer);
        dataset.Add(DicomTag.ReferringPhysicianName, "");
        dataset.Add(DicomTag.StudyDescription, patientMetadata.StudyDescription ?? "");
        dataset.Add(DicomTag.SeriesDescription, patientMetadata.SeriesDescription ?? "");

        // Patient Module
        dataset.Add(DicomTag.PatientName, patientMetadata.PatientName);
        dataset.Add(DicomTag.PatientID, patientMetadata.PatientId);

        if (!string.IsNullOrWhiteSpace(patientMetadata.PatientBirthDate))
        {
            var birthDate = patientMetadata.GetDicomBirthDate();
            if (!string.IsNullOrEmpty(birthDate))
                dataset.Add(DicomTag.PatientBirthDate, birthDate);
        }
        else
        {
            dataset.Add(DicomTag.PatientBirthDate, "19700101"); // Default like working files
        }

        dataset.Add(DicomTag.PatientSex, patientMetadata.PatientSex ?? "");

        // Cine Module - set to 0 like working files (video duration encoded in MP4)
        dataset.Add(DicomTag.CineRate, "0");
        dataset.Add(DicomTag.FrameTime, "0");

        // Study/Series Instance UIDs
        dataset.Add(DicomTag.StudyInstanceUID, UidGenerator.GenerateStudyUid());
        dataset.Add(DicomTag.SeriesInstanceUID, UidGenerator.GenerateSeriesUid());
        dataset.Add(DicomTag.StudyID, "");
        dataset.Add(DicomTag.SeriesNumber, 1);
        dataset.Add(DicomTag.InstanceNumber, 1);

        // Image Pixel Module - set dimensions to 0 like working files
        dataset.Add(DicomTag.SamplesPerPixel, (ushort)3);
        dataset.Add(DicomTag.PhotometricInterpretation, "YBR_PARTIAL_420");
        dataset.Add(DicomTag.PlanarConfiguration, (ushort)0);
        dataset.Add(DicomTag.NumberOfFrames, "0"); // Video frame count is in MP4 container
        dataset.Add(DicomTag.FrameIncrementPointer, DicomTag.FrameTime); // Points to FrameTime tag
        dataset.Add(DicomTag.Rows, (ushort)0); // Set to 0 like working files
        dataset.Add(DicomTag.Columns, (ushort)0); // Set to 0 like working files
        dataset.Add(DicomTag.BitsAllocated, (ushort)8);
        dataset.Add(DicomTag.BitsStored, (ushort)8);
        dataset.Add(DicomTag.HighBit, (ushort)7);
        dataset.Add(DicomTag.PixelRepresentation, (ushort)0);
        dataset.Add(DicomTag.LossyImageCompression, "01"); // Lossy compression

        // Equipment Module
        dataset.Add(DicomTag.ManufacturerModelName, Constants.AppName);
        dataset.Add(DicomTag.SoftwareVersions, Constants.AppVersion);
        dataset.Add(DicomTag.StationName, "");
        dataset.Add(DicomTag.InstitutionName, "");

        // Secondary Capture Module (required for video DICOM)
        dataset.Add(DicomTag.DateOfSecondaryCapture, PatientMetadata.FormatDicomDate(patientMetadata.StudyDate));
        dataset.Add(DicomTag.TimeOfSecondaryCapture, PatientMetadata.FormatDicomTime(patientMetadata.StudyDate));
        dataset.Add(DicomTag.SecondaryCaptureDeviceManufacturer, Constants.Manufacturer);
        dataset.Add(DicomTag.SecondaryCaptureDeviceManufacturerModelName, Constants.AppName);
        dataset.Add(DicomTag.SecondaryCaptureDeviceSoftwareVersions, Constants.AppVersion);

        // Additional timing/duration elements
        dataset.Add(DicomTag.EffectiveDuration, "0");
        dataset.Add(DicomTag.SeriesDate, PatientMetadata.FormatDicomDate(patientMetadata.StudyDate));
        dataset.Add(DicomTag.SeriesTime, PatientMetadata.FormatDicomTime(patientMetadata.StudyDate));

        // Performing Physician
        if (!string.IsNullOrWhiteSpace(patientMetadata.PerformingPhysicianName))
            dataset.Add(DicomTag.PerformingPhysicianName, patientMetadata.PerformingPhysicianName);

        dataset.Add(DicomTag.OperatorsName, "");
        dataset.Add(DicomTag.PatientOrientation, "");

        // Acquisition Context (empty sequence, but required for some viewers)
        dataset.Add(DicomTag.AcquisitionContextSequence, new DicomSequence(DicomTag.AcquisitionContextSequence));

        return new DicomFile(dataset);
    }

    /// <summary>
    /// Adds H.264 video data to DICOM file as encapsulated pixel data
    /// Uses proper DICOM encapsulation for video streams
    /// </summary>
    private void AddVideoPixelData(DicomFile dicomFile, byte[] videoData)
    {
        var dataset = dicomFile.Dataset;

        // Set transfer syntax to MPEG-4 AVC/H.264 High Profile
        dicomFile.FileMetaInfo.TransferSyntax = DicomTransferSyntax.Lookup(DicomUID.Parse(Constants.Mpeg4TransferSyntaxUid));

        // For DICOM video, the entire H.264 stream is stored as a single frame
        // with the video data encapsulated directly
        var pixelData = new DicomOtherByteFragment(DicomTag.PixelData);

        // Add offset table (empty for single-frame video)
        pixelData.Fragments.Add(new MemoryByteBuffer(Array.Empty<byte>()));

        // Add the entire video as a single fragment
        // DICOM video stores the complete H.264 bitstream as one encapsulated frame
        pixelData.Fragments.Add(new MemoryByteBuffer(videoData));

        dataset.AddOrUpdate(pixelData);
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
