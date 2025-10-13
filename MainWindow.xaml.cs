using FellowOakDicom;
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
        private const int TARGET_FPS = 30;             // Browser/eUnity-friendly
        private const bool USE_BD_COMPAT = true;       // true => TS 1.2.840.10008.1.2.4.103 (BD-compatible), false => 4.102
        private const bool ADD_NUMBER_OF_FRAMES = true;// Add NumberOfFrames (some viewers expect it)
        private const string MODALITY = "ES";          // "ES" endoscopy, or "XC" if you swap SOP to Video Photographic
        private const int FRAGMENT_BYTES = 256 * 1024; // 256 KB fragments (smaller = safer through gateways)

        private string selectedFile = "";
        private string lastDicomPath = "";
        private Process process;

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

                // Get source duration (for NumberOfFrames estimate post-transcode)
                statusText.Text = "Probing source duration…";
                progressBar.Value = 16;
                double srcDurationSec = await GetDurationSecondsAsync(selectedFile, ffprobe, ffmpeg); // robust probe

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

                // Probe dimensions from the **raw** stream (fps is forced)
                var (w, h, _) = ProbeVideo(ffmpeg, preppedRaw);
                if (w <= 0 || h <= 0) { w = 1280; h = 720; }

                // Estimate NumberOfFrames from the *source* duration and our CFR target.
                // (Using source duration is typically good enough; viewers mainly need a non-zero integer.)
                int estFrames = Math.Max(1, (int)Math.Round(srcDurationSec * TARGET_FPS, MidpointRounding.AwayFromZero));

                statusText.Text = "Wrapping as DICOM (Video Endoscopic Image Storage)…";
                progressBar.Value = 85;

                double fps = TARGET_FPS;
                int cineRate = TARGET_FPS;

                lastDicomPath = MakeDicomVideoFromRaw(
                    preppedRaw, IOPath.GetDirectoryName(selectedFile)!, w, h, fps, cineRate, estFrames);

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
                process.ErrorDataReceived += (s, ev) => { /* ffmpeg progress on stderr if needed */ };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new Exception($"{IOPath.GetFileName(fileName)} exited with code {process.ExitCode}");
            });
        }

        // Try ffprobe first; if missing, parse "Duration: HH:MM:SS.xx" from ffmpeg -i stderr.
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

            // Fallback: use ffmpeg to print metadata and parse "Duration:" line
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

            // Worst-case default (keeps things moving)
            return 1.0; // 1 second
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

        private string MakeDicomVideoFromRaw(string rawPath, string outFolder, int width, int height, double fps, int cineRate, int estimatedFrames)
        {
            // SOP Class: Video Endoscopic Image Storage
            var sopClassUid = DicomUID.Parse("1.2.840.10008.5.1.4.1.1.77.1.1.1");
            // Transfer Syntax: BD-compatible (4.103) or generic (4.102)
            var tsUid = USE_BD_COMPAT ? "1.2.840.10008.1.2.4.103" : "1.2.840.10008.1.2.4.102";
            var ts = DicomTransferSyntax.Lookup(DicomUID.Parse(tsUid));

            var now = DateTime.Now;

            var ds = new DicomDataset(ts)
            {
                { DicomTag.SOPClassUID, sopClassUid },
                { DicomTag.SOPInstanceUID, DicomUID.Generate() },
                { DicomTag.StudyInstanceUID, DicomUID.Generate() },
                { DicomTag.SeriesInstanceUID, DicomUID.Generate() },

                { DicomTag.PatientName, "DICOMIZER^TEST" },
                { DicomTag.PatientID, "DICOMizer" },

                { DicomTag.InstanceCreationDate, now.ToString("yyyyMMdd") },
                { DicomTag.InstanceCreationTime, now.ToString("HHmmss") },
                { DicomTag.StudyDate, now.ToString("yyyyMMdd") },
                { DicomTag.StudyTime, now.ToString("HHmmss") },
                { DicomTag.SeriesDate, now.ToString("yyyyMMdd") },
                { DicomTag.SeriesTime, now.ToString("HHmmss") },
                { DicomTag.ContentDate, now.ToString("yyyyMMdd") },
                { DicomTag.ContentTime, now.ToString("HHmmss") },
                { DicomTag.SpecificCharacterSet, "ISO_IR 100" },

                { DicomTag.Modality, MODALITY },
                { DicomTag.SeriesNumber, 1 },
                { DicomTag.InstanceNumber, 1 },

                { DicomTag.StudyDescription, MODALITY == "XC" ? "Photographic Video" : "Endoscopy Video" },
                { DicomTag.SeriesDescription, USE_BD_COMPAT ? "H.264 BD-Compatible HP@L4.1" : "H.264 HP@L4.1" },
                { DicomTag.ImageType, new[] { "ORIGINAL", "PRIMARY" } },

                // Required image attributes for Video IODs
                { DicomTag.Rows, (ushort)height },
                { DicomTag.Columns, (ushort)width },
                { DicomTag.SamplesPerPixel, (ushort)3 },
                { DicomTag.PhotometricInterpretation, "YBR_PARTIAL_420" },
                { DicomTag.BitsAllocated, (ushort)8 },
                { DicomTag.BitsStored, (ushort)8 },
                { DicomTag.HighBit, (ushort)7 },
                { DicomTag.PixelRepresentation, (ushort)0 },

                // Pixel aspect & timing (match our stream)
                { DicomTag.PixelAspectRatio, "1\\1" },                        // (0028,0034) square pixels
                { DicomTag.VideoImageFormatAcquired, "NTSC" },               // (0018,1022)
                { DicomTag.CineRate, cineRate },                             // (0018,0040)
                { DicomTag.FrameTime, 1000.0 / fps },                        // (0018,1063) ms
                { DicomTag.RecommendedDisplayFrameRateInFloat, (float)fps }, // (0008,2145)

                // Helpful pointer
                { DicomTag.FrameIncrementPointer, new DicomTag[] { DicomTag.FrameTime } },

                // Viewer-friendly flags
                { DicomTag.BurnedInAnnotation, "NO" },
                { DicomTag.LossyImageCompression, "01" },
                { DicomTag.LossyImageCompressionMethod, "ISO_14496_10" }
            };

            // Compressed data rules — ensure these are absent/handled
            ds.Remove(DicomTag.PlanarConfiguration);

            if (ADD_NUMBER_OF_FRAMES)
            {
                // Some viewers insist on this even for encapsulated video streams
                ds.AddOrUpdate(DicomTag.NumberOfFrames, estimatedFrames.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                ds.Remove(DicomTag.NumberOfFrames);
            }

            // === Multi-fragment Pixel Data using DicomOtherByteFragment ===
            var frag = new DicomOtherByteFragment(DicomTag.PixelData);

            // Do NOT add your own empty BOT; fo-dicom writes a single empty BOT automatically.
            using (var fs = new FileStream(rawPath, FileMode.Open, FileAccess.Read))
            {
                var buffer = new byte[FRAGMENT_BYTES];
                int read;
                while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (read == buffer.Length)
                    {
                        frag.Fragments.Add(new MemoryByteBuffer(buffer));
                        buffer = new byte[FRAGMENT_BYTES]; // new buffer for next read
                    }
                    else
                    {
                        var last = new byte[read];
                        Buffer.BlockCopy(buffer, 0, last, 0, read);
                        frag.Fragments.Add(new MemoryByteBuffer(last));
                    }
                }
            }

            // Attach Pixel Data
            ds.Add(frag);

            // File Meta — must match dataset TS
            var file = new DicomFile(ds);
            file.FileMetaInfo.MediaStorageSOPClassUID = sopClassUid;
            file.FileMetaInfo.MediaStorageSOPInstanceUID = ds.GetSingleValue<DicomUID>(DicomTag.SOPInstanceUID);
            file.FileMetaInfo.TransferSyntax = ts;
            file.FileMetaInfo.ImplementationClassUID = DicomImplementation.ClassUID;
            file.FileMetaInfo.ImplementationVersionName = DicomImplementation.Version;

            var outName = IOPath.GetFileNameWithoutExtension(rawPath) + ".dcm";
            var outPath = IOPath.Combine(outFolder, outName);
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
