using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using IOPath = System.IO.Path;

namespace DICOMizer
{
    public partial class MainWindow : Window
    {
        // ==== Tunables (safe defaults for eUnity) ====
        private const int TARGET_FPS = 30;               // Browser/eUnity-friendly
        private const bool USE_BD_COMPAT = true;         // true => TS 1.2.840.10008.1.2.4.103 (BD-compatible), false => 4.102
        private const bool ADD_NUMBER_OF_FRAMES = false; // Only set true if you compute exact frame count
        private const string MODALITY = "ES";            // "ES" endoscopy, or "XC" if you swap SOP to Video Photographic

        private string selectedFile = "";
        private string lastDicomPath = "";
        private Process? process;

        private string ToolPath(string relative) =>
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relative);
        private string DcmtkExe(string name) => ToolPath(System.IO.Path.Combine("Tools", "dcmtk", "bin", name));
        private string FfmpegExe() => ToolPath(System.IO.Path.Combine("Tools", "ffmpeg", "bin", "ffmpeg.exe"));
        private string FfprobeExe() => ToolPath(System.IO.Path.Combine("Tools", "ffmpeg", "bin", "ffprobe.exe"));

        public MainWindow() { InitializeComponent(); }

        private void ChooseFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Video Files|*.mp4;*.avi;*.mov;*.mkv;*.mpeg;*.mpg" };
            if (dlg.ShowDialog() == true)
            {
                selectedFile = dlg.FileName;
                lastDicomPath = "";
                statusText.Text = $"Selected: {selectedFile}";
            }
        }

        private async void Convert_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedFile))
            {
                MessageBox.Show("Please choose a file first.");
                return;
            }

            string dcmdump = DcmtkExe("dcmdump.exe");
            string ffmpeg = FfmpegExe();
            string ffprobe = FfprobeExe();

            if (!File.Exists(dcmdump)) { MessageBox.Show("dcmdump.exe not found at: " + dcmdump); return; }
            if (!File.Exists(ffmpeg)) { MessageBox.Show("ffmpeg.exe not found at: " + ffmpeg); return; }

            try
            {
                statusText.Text = "Smoke test (dcmdump --version)…";
                progressBar.Value = 8;
                await RunProcessAsync(dcmdump, "--version");

                statusText.Text = "Probing source duration…";
                progressBar.Value = 16;
                double _ = await GetDurationSecondsAsync(selectedFile, ffprobe, ffmpeg); // We won't guess NumberOfFrames from this

                statusText.Text = "Transcoding to raw Annex-B H.264 (High@L4.1)…";
                progressBar.Value = 40;

                string tempDir = IOPath.Combine(IOPath.GetDirectoryName(selectedFile)!, "DICOMizerTemp");
                Directory.CreateDirectory(tempDir);

                string preppedRaw = IOPath.Combine(
                    tempDir, IOPath.GetFileNameWithoutExtension(selectedFile) + "_prepped.h264");

                // CFR 30, yuv420p, High@L4.1, closed GOP, no B-frames, AUD+repeat headers, Annex-B, square pixels.
                int keyint = TARGET_FPS * 2; // 2 seconds
                string ffArgs =
                    $"-y -i \"{selectedFile}\" -an " +
                    $"-vf \"fps={TARGET_FPS},setsar=1\" " +
                    $"-pix_fmt yuv420p -c:v libx264 -profile:v high -level:v 4.1 " +
                    $"-r {TARGET_FPS} -vsync cfr -g {keyint} -keyint_min {keyint} -sc_threshold 0 -bf 0 " +
                    $"-preset veryfast -crf 20 -x264-params \"aud=1:repeat-headers=1:open-gop=0\" " +
                    $"-bsf:v h264_mp4toannexb -f h264 \"{preppedRaw}\"";

                await RunProcessAsync(ffmpeg, ffArgs);

                var (w, h, _) = ProbeVideo(ffmpeg, preppedRaw);
                if (w <= 0 || h <= 0) { w = 1280; h = 720; }

                statusText.Text = "Wrapping as DICOM (Video Endoscopic Image Storage)…";
                progressBar.Value = 85;

                double fps = TARGET_FPS;
                int cineRate = TARGET_FPS;

                // We won't add NumberOfFrames unless computed exactly.
                int exactFrames = 0;
                if (ADD_NUMBER_OF_FRAMES)
                    exactFrames = await TryGetExactFrameCountAsync(FfprobeExe(), selectedFile);

                // Get patient ID and accession number from UI
                string patientId = "";
                string accessionNumber = "";
                Dispatcher.Invoke(() =>
                {
                    patientId = patientIdTextBox.Text;
                    accessionNumber = accessionNumberTextBox.Text;
                });

                lastDicomPath = MakeDicomVideoFromRaw(
                    preppedRaw, IOPath.GetDirectoryName(selectedFile)!, w, h, fps, cineRate, exactFrames, patientId, accessionNumber);

                progressBar.Value = 100;
                statusText.Text = $"Done. DICOM saved:\n{lastDicomPath}";
            }
            catch (Exception ex)
            {
                statusText.Text = "Error: " + ex.Message;
            }
        }

        private Task RunProcessAsync(string fileName, string arguments)
        {
            return Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                process = new Process { StartInfo = psi };
                process.OutputDataReceived += (s, ev) =>
                {
                    if (!string.IsNullOrWhiteSpace(ev.Data))
                        Dispatcher.Invoke(() => statusText.Text = ev.Data);
                };
                process.ErrorDataReceived += (s, ev) => { /* ffmpeg progress is on stderr; ignore */ };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new Exception($"{IOPath.GetFileName(fileName)} exited with code {process.ExitCode}");
            });
        }

        private async Task<double> GetDurationSecondsAsync(string path, string ffprobeExe, string ffmpegExe)
        {
            if (File.Exists(ffprobeExe))
            {
                try
                {
                    string args = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{path}\"";
                    string outText = await RunProcessCaptureStdoutAsync(ffprobeExe, args);
                    if (double.TryParse(outText.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double dur) && dur > 0)
                        return dur;
                }
                catch { /* fall through to ffmpeg parser */ }
            }

            try
            {
                string stderr = await RunProcessCaptureStderrAsync(ffmpegExe, $"-hide_banner -i \"{path}\"");
                var m = Regex.Match(stderr, @"Duration:\s*(\d{2}):(\d{2}):(\d{2}\.\d+)");
                if (m.Success)
                {
                    int hh = int.Parse(m.Groups[1].Value);
                    int mm = int.Parse(m.Groups[2].Value);
                    double ss = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                    return hh * 3600 + mm * 60 + ss;
                }
            }
            catch { }
            return 1.0; // fallback default
        }

        private async Task<int> TryGetExactFrameCountAsync(string ffprobeExe, string videoPath)
        {
            if (!File.Exists(ffprobeExe)) return 0;
            try
            {
                string args = $"-v error -count_frames -select_streams v:0 -show_entries stream=nb_read_frames " +
                              "-of default=nokey=1:noprint_wrappers=1 " +
                              $"\"{videoPath}\"";
                string outText = await RunProcessCaptureStdoutAsync(ffprobeExe, args);
                if (int.TryParse(outText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n > 0)
                    return n;
            }
            catch { }
            return 0;
        }

        private Task<string> RunProcessCaptureStdoutAsync(string fileName, string arguments)
        {
            return Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var p = new Process { StartInfo = psi };
                p.Start();
                string stdout = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0) throw new Exception($"{IOPath.GetFileName(fileName)} exited with code {p.ExitCode}");
                return stdout;
            });
        }

        private Task<string> RunProcessCaptureStderrAsync(string fileName, string arguments)
        {
            return Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using var p = new Process { StartInfo = psi };
                p.Start();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();
                return stderr;
            });
        }

        private (int width, int height, double fps) ProbeVideo(string ffmpegExe, string videoPath)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegExe,
                Arguments = $"-hide_banner -i \"{videoPath}\"",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var p = new Process { StartInfo = psi };
            p.Start();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();

            var rxWH = new Regex(@"\b(?<w>\d{2,5})x(?<h>\d{2,5})\b");
            var rxFPS = new Regex(@"\b(?<fps>\d+(\.\d+)?)\s*fps\b", RegexOptions.IgnoreCase);

            int width = 0, height = 0; double fps = 0;
            var m = rxWH.Match(stderr);
            if (m.Success) { int.TryParse(m.Groups["w"].Value, out width); int.TryParse(m.Groups["h"].Value, out height); }
            var f = rxFPS.Match(stderr);
            if (f.Success) double.TryParse(f.Groups["fps"].Value, out fps);

            return (width, height, fps);
        }

        private string MakeDicomVideoFromRaw(string rawPath, string outFolder, int width, int height, double fps, int cineRate, int exactFrames, string patientId, string accessionNumber)
        {
            // SOP Class: Video Endoscopic Image Storage
            var sopClassUid = DicomUID.Parse("1.2.840.10008.5.1.4.1.1.77.1.1.1");
            // Transfer Syntax: BD-compatible (4.103) or generic (4.102)
            var tsUid = USE_BD_COMPAT ? "1.2.840.10008.1.2.4.103" : "1.2.840.10008.1.2.4.102";
            var ts = DicomTransferSyntax.Lookup(DicomUID.Parse(tsUid));

            var now = DateTime.Now;

            // Use provided patient ID or default
            string patId = string.IsNullOrWhiteSpace(patientId) ? "DICOMizer" : patientId;
            string accNum = string.IsNullOrWhiteSpace(accessionNumber) ? "" : accessionNumber;

            // Generate unique Study ID for PACS
            string studyId = DateTime.Now.ToString("yyyyMMddHHmmss");

            // Build dataset (set all descriptive pixel attrs here, not on DicomPixelData)
            var ds = new DicomDataset
            {
                { DicomTag.SOPClassUID, sopClassUid },
                { DicomTag.SOPInstanceUID, DicomUID.Generate() },
                { DicomTag.StudyInstanceUID, DicomUID.Generate() },
                { DicomTag.SeriesInstanceUID, DicomUID.Generate() },

                // Patient Module - REQUIRED for PACS
                { DicomTag.PatientName, patId },
                { DicomTag.PatientID, patId },
                { DicomTag.PatientBirthDate, "" },
                { DicomTag.PatientSex, "" },

                // General Study Module - REQUIRED for PACS
                { DicomTag.StudyID, studyId },
                { DicomTag.StudyDate, now.ToString("yyyyMMdd") },
                { DicomTag.StudyTime, now.ToString("HHmmss") },
                { DicomTag.ReferringPhysicianName, "" },
                { DicomTag.AccessionNumber, accNum },
                { DicomTag.StudyDescription, "Endoscopy Video" },

                // General Series Module - REQUIRED
                { DicomTag.Modality, MODALITY },
                { DicomTag.SeriesNumber, "1" },
                { DicomTag.SeriesDate, now.ToString("yyyyMMdd") },
                { DicomTag.SeriesTime, now.ToString("HHmmss") },
                { DicomTag.SeriesDescription, USE_BD_COMPAT ? "H.264 BD-Compatible HP@L4.1" : "H.264 HP@L4.1" },

                // General Equipment Module
                { DicomTag.Manufacturer, "DICOMizer" },

                // General Image Module
                { DicomTag.InstanceNumber, "1" },
                { DicomTag.InstanceCreationDate, now.ToString("yyyyMMdd") },
                { DicomTag.InstanceCreationTime, now.ToString("HHmmss") },
                { DicomTag.ContentDate, now.ToString("yyyyMMdd") },
                { DicomTag.ContentTime, now.ToString("HHmmss") },
                { DicomTag.ImageType, new[] { "ORIGINAL", "PRIMARY" } },

                { DicomTag.SpecificCharacterSet, "ISO_IR 100" },

                // Required image attributes for Video IODs (compressed)
                { DicomTag.Rows, (ushort)height },
                { DicomTag.Columns, (ushort)width },
                { DicomTag.SamplesPerPixel, (ushort)3 },
                { DicomTag.PhotometricInterpretation, "YBR_PARTIAL_420" },
                { DicomTag.BitsAllocated, (ushort)8 },
                { DicomTag.BitsStored, (ushort)8 },
                { DicomTag.HighBit, (ushort)7 },
                { DicomTag.PixelRepresentation, (ushort)0 },

                // Pixel aspect & timing
                { DicomTag.PixelAspectRatio, "1\\1" },
                { DicomTag.CineRate, cineRate.ToString(CultureInfo.InvariantCulture) },  // (0018,0040) IS type = string
                { DicomTag.FrameTime, 1000.0 / fps },                                     // (0018,1063) DS type = decimal string
                { DicomTag.RecommendedDisplayFrameRate, Math.Round(fps).ToString(CultureInfo.InvariantCulture) }, // (0008,2144) IS type = string
                { DicomTag.RecommendedDisplayFrameRateInFloat, (float)fps },              // (0008,2145) FL type = float
                { DicomTag.FrameIncrementPointer, new DicomTag[] { DicomTag.FrameTime } },

                // Viewer-friendly hints
                { DicomTag.BurnedInAnnotation, "NO" },
                { DicomTag.LossyImageCompression, "01" },
                { DicomTag.LossyImageCompressionMethod, "ISO_14496_10" }
            };

            // No PlanarConfiguration for compressed syntax
            ds.Remove(DicomTag.PlanarConfiguration);

            // Only add NumberOfFrames if exact (avoid mismatch)
            ds.Remove(DicomTag.NumberOfFrames);
            if (ADD_NUMBER_OF_FRAMES && exactFrames > 0)
                ds.AddOrUpdate(DicomTag.NumberOfFrames, exactFrames.ToString(CultureInfo.InvariantCulture));

            // === Encapsulated Pixel Data via DicomPixelData (compressed=true) ===
            var pixelData = DicomPixelData.Create(ds, true); // compressed
            byte[] bitstream = File.ReadAllBytes(rawPath);

            // Ensure even length to avoid DICOM warnings
            if (bitstream.Length % 2 != 0)
            {
                byte[] padded = new byte[bitstream.Length + 1];
                Array.Copy(bitstream, padded, bitstream.Length);
                padded[bitstream.Length] = 0; // pad with null byte
                bitstream = padded;
            }

            // Single fragment = safest for web viewers (don't split NALs)
            pixelData.AddFrame(new MemoryByteBuffer(bitstream));

            // File Meta — must match dataset TS
            var file = new DicomFile(ds);
            file.FileMetaInfo.MediaStorageSOPClassUID = sopClassUid;
            file.FileMetaInfo.MediaStorageSOPInstanceUID = ds.GetSingleValue<DicomUID>(DicomTag.SOPInstanceUID);
            file.FileMetaInfo.TransferSyntax = ts;
            file.FileMetaInfo.ImplementationClassUID = DicomImplementation.ClassUID;
            file.FileMetaInfo.ImplementationVersionName = DicomImplementation.Version;

            var outName = Path.GetFileNameWithoutExtension(rawPath) + ".dcm";
            var outPath = Path.Combine(outFolder, outName);
            file.Save(outPath);
            return outPath;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (process != null && !process.HasExited)
            {
                process.Kill();
                statusText.Text = "Conversion cancelled.";
            }
        }

        private void OpenOutput_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = string.IsNullOrEmpty(lastDicomPath)
                    ? IOPath.Combine(IOPath.GetDirectoryName(selectedFile) ?? "", IOPath.GetFileNameWithoutExtension(selectedFile) + "_prepped.dcm")
                    : lastDicomPath;

                var folder = IOPath.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
                else
                    MessageBox.Show("Output folder not found.");
            }
            catch (Exception ex)
            {
                statusText.Text = "OpenOutput error: " + ex.Message;
            }
        }

        private void Dump_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dcm = string.IsNullOrEmpty(lastDicomPath)
                    ? IOPath.Combine(IOPath.GetDirectoryName(selectedFile) ?? "", IOPath.GetFileNameWithoutExtension(selectedFile) + "_prepped.dcm")
                    : lastDicomPath;

                if (!File.Exists(dcm)) { MessageBox.Show("DICOM not found: " + dcm); return; }

                var dumpTxt = dcm + ".txt";
                var dcmdump = DcmtkExe("dcmdump.exe");
                var psi = new ProcessStartInfo
                {
                    FileName = dcmdump,
                    Arguments = $"\"{dcm}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                var text = p!.StandardOutput.ReadToEnd();
                p.WaitForExit();
                File.WriteAllText(dumpTxt, text);
                statusText.Text = $"Dump saved: {dumpTxt}";
                Process.Start(new ProcessStartInfo("notepad.exe", $"\"{dumpTxt}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                statusText.Text = "Dump error: " + ex.Message;
            }
        }
    }
}
