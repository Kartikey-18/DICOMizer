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
    /// Format matches working eUnity-compatible DICOM video files exactly
    /// Based on dump of working file: 1.3.6.1.4.1.48948.18245443431773.44441556691258.3.DCM
    /// </summary>
    private DicomFile CreateDicomDataset(VideoMetadata videoMetadata, PatientMetadata patientMetadata)
    {
        var dataset = new DicomDataset();

        // (0008,0008) ImageType
        dataset.Add(DicomTag.ImageType, "ORIGINAL", "SECONDARY");
        // (0008,0016) SOPClassUID
        dataset.Add(DicomTag.SOPClassUID, Constants.VideoEndoscopicSopClassUid);
        // (0008,0018) SOPInstanceUID
        dataset.Add(DicomTag.SOPInstanceUID, UidGenerator.GenerateSopInstanceUid());
        // (0008,0020) StudyDate
        dataset.Add(DicomTag.StudyDate, PatientMetadata.FormatDicomDate(patientMetadata.StudyDate));
        // (0008,0021) SeriesDate - empty in working file
        dataset.Add(DicomTag.SeriesDate, "");
        // (0008,0030) StudyTime
        dataset.Add(DicomTag.StudyTime, patientMetadata.StudyDate.ToString("HHmmss.ffffff"));
        // (0008,0031) SeriesTime - empty in working file
        dataset.Add(DicomTag.SeriesTime, "");
        // (0008,0050) AccessionNumber
        dataset.Add(DicomTag.AccessionNumber, patientMetadata.AccessionNumber ?? "");
        // (0008,0060) Modality
        dataset.Add(DicomTag.Modality, "ES");
        // (0008,0064) ConversionType
        dataset.Add(DicomTag.ConversionType, "DV");
        // (0008,0070) Manufacturer
        dataset.Add(DicomTag.Manufacturer, Constants.Manufacturer);
        // (0008,0080) InstitutionName
        dataset.Add(DicomTag.InstitutionName, "");
        // (0008,0090) ReferringPhysicianName
        dataset.Add(DicomTag.ReferringPhysicianName, "");
        // (0008,0100) CodeValue - present in working file as empty
        dataset.Add(DicomTag.CodeValue, "");
        // (0008,1010) StationName
        dataset.Add(DicomTag.StationName, Environment.MachineName);
        // (0008,1030) StudyDescription
        dataset.Add(DicomTag.StudyDescription, patientMetadata.StudyDescription ?? "");
        // (0008,103E) SeriesDescription
        dataset.Add(DicomTag.SeriesDescription, patientMetadata.SeriesDescription ?? "");
        // (0008,1040) InstitutionalDepartmentName
        dataset.Add(DicomTag.InstitutionalDepartmentName, "");
        // (0008,1050) PerformingPhysicianName
        dataset.Add(DicomTag.PerformingPhysicianName, patientMetadata.PerformingPhysicianName ?? "");
        // (0008,1070) OperatorsName
        dataset.Add(DicomTag.OperatorsName, "");
        // (0008,1090) ManufacturerModelName
        dataset.Add(DicomTag.ManufacturerModelName, Constants.AppName);

        // (0010,0010) PatientName
        dataset.Add(DicomTag.PatientName, patientMetadata.PatientName);
        // (0010,0020) PatientID
        dataset.Add(DicomTag.PatientID, patientMetadata.PatientId);
        // (0010,0030) PatientBirthDate
        if (!string.IsNullOrWhiteSpace(patientMetadata.PatientBirthDate))
        {
            var birthDate = patientMetadata.GetDicomBirthDate();
            if (!string.IsNullOrEmpty(birthDate))
                dataset.Add(DicomTag.PatientBirthDate, birthDate);
            else
                dataset.Add(DicomTag.PatientBirthDate, "19700101");
        }
        else
        {
            dataset.Add(DicomTag.PatientBirthDate, "19700101");
        }
        // (0010,0040) PatientSex
        dataset.Add(DicomTag.PatientSex, patientMetadata.PatientSex ?? "");
        // (0010,21B0) AdditionalPatientHistory
        dataset.Add(DicomTag.AdditionalPatientHistory, "");

        // (0018,0040) CineRate - IS type, value "0"
        dataset.Add(DicomTag.CineRate, "0");
        // (0018,0072) EffectiveDuration - DS type, value "0.0" like working file
        dataset.Add(DicomTag.EffectiveDuration, 0.0m);
        // (0018,1012) DateOfSecondaryCapture
        dataset.Add(DicomTag.DateOfSecondaryCapture, PatientMetadata.FormatDicomDate(patientMetadata.StudyDate));
        // (0018,1014) TimeOfSecondaryCapture - HHMMSS format without microseconds
        dataset.Add(DicomTag.TimeOfSecondaryCapture, patientMetadata.StudyDate.ToString("HHmmss"));
        // (0018,1016) SecondaryCaptureDeviceManufacturer
        dataset.Add(DicomTag.SecondaryCaptureDeviceManufacturer, Constants.Manufacturer);
        // (0018,1018) SecondaryCaptureDeviceManufacturerModelName
        dataset.Add(DicomTag.SecondaryCaptureDeviceManufacturerModelName, Constants.AppName);
        // (0018,1019) SecondaryCaptureDeviceSoftwareVersions
        dataset.Add(DicomTag.SecondaryCaptureDeviceSoftwareVersions, Constants.AppVersion);
        // (0018,1063) FrameTime - DS type, value "0.0" like working file
        dataset.Add(DicomTag.FrameTime, 0.0m);

        // (0020,000D) StudyInstanceUID
        dataset.Add(DicomTag.StudyInstanceUID, UidGenerator.GenerateStudyUid());
        // (0020,000E) SeriesInstanceUID
        dataset.Add(DicomTag.SeriesInstanceUID, UidGenerator.GenerateSeriesUid());
        // (0020,0010) StudyID
        dataset.Add(DicomTag.StudyID, "");
        // (0020,0011) SeriesNumber
        dataset.Add(DicomTag.SeriesNumber, 1);
        // (0020,0013) InstanceNumber
        dataset.Add(DicomTag.InstanceNumber, 1);
        // (0020,0020) PatientOrientation
        dataset.Add(DicomTag.PatientOrientation, "");

        // (0028,0002) SamplesPerPixel
        dataset.Add(DicomTag.SamplesPerPixel, (ushort)3);
        // (0028,0004) PhotometricInterpretation
        dataset.Add(DicomTag.PhotometricInterpretation, "YBR_PARTIAL_420");
        // (0028,0006) PlanarConfiguration
        dataset.Add(DicomTag.PlanarConfiguration, (ushort)0);
        // (0028,0008) NumberOfFrames
        dataset.Add(DicomTag.NumberOfFrames, "0");
        // (0028,0009) FrameIncrementPointer - points to FrameTime (0018,1063)
        dataset.Add(DicomTag.FrameIncrementPointer, DicomTag.FrameTime);
        // (0028,0010) Rows
        dataset.Add(DicomTag.Rows, (ushort)0);
        // (0028,0011) Columns
        dataset.Add(DicomTag.Columns, (ushort)0);
        // (0028,0100) BitsAllocated
        dataset.Add(DicomTag.BitsAllocated, (ushort)8);
        // (0028,0101) BitsStored
        dataset.Add(DicomTag.BitsStored, (ushort)8);
        // (0028,0102) HighBit
        dataset.Add(DicomTag.HighBit, (ushort)7);
        // (0028,0103) PixelRepresentation
        dataset.Add(DicomTag.PixelRepresentation, (ushort)0);
        // (0028,2110) LossyImageCompression
        dataset.Add(DicomTag.LossyImageCompression, "01");

        // (0040,0555) AcquisitionContextSequence - empty sequence
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
