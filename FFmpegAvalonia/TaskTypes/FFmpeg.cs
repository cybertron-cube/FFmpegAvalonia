using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ExtensionMethods;
using System.Threading;
using FFmpegAvalonia.ViewModels;
using System.Collections.ObjectModel;
using PCLUntils.IEnumerables;
using System.Linq;
using FFmpegAvalonia.Models;

namespace FFmpegAvalonia.TaskTypes
{
    public class FFmpeg
    {
        private FFmpegProcess _FFProcess;
        private readonly string _FFmpegPath;
        private readonly ConcurrentDictionary<string, int> _FilesDict;
        private int _LastFrame;
        private long _CancelQ = 0;
        public bool CancelQ
        {
            get => Interlocked.Read(ref _CancelQ) == 1;
            set => Interlocked.Exchange(ref _CancelQ, Convert.ToInt64(value));
        }
        private int _TotalPrevFrameProgress;
        private int _TotalDirFrames;
        private double _EndTime;
        private IProgress<double>? _UIProgress;
        private readonly object _DisposeLock = new();
        private string _LastStdErrLine = String.Empty;
        private MainWindowViewModel? _ViewModel;
        private bool _SkipFile;
        public FFmpeg(string ffmpegdir)
        {
            _FFmpegPath = ffmpegdir;
            _FFProcess = new FFmpegProcess(_FFmpegPath)
            {
                StartInfo = DefaultStartInfo(),
                EnableRaisingEvents = true,
            };
            _FilesDict = new ConcurrentDictionary<string, int>();
        }
        private static ProcessStartInfo DefaultStartInfo()
        {
            return new ProcessStartInfo
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
        }
        private void NewFFProcess()
        {
            _FFProcess = new FFmpegProcess(_FFmpegPath)
            {
                StartInfo = DefaultStartInfo(),
                EnableRaisingEvents = true
            };
        }
        public string GetFrameCountApproximate(string dir, string searchPattern, string args)
        {
            bool skipFrameRateCalc = false;
            decimal frameRate = 0;
            Regex re = new("(?:\\s+-r\\s+)(\\S+)");
            Match match = re.Match(args);
            if (match.Success)
            {
                skipFrameRateCalc = true;
                Trace.TraceInformation("Skipping per file framerate calculation because -r arg exists");
                string frameRateToParse = match.Groups[1].Value;
                var split = frameRateToParse.Split("/");
                decimal numerator = decimal.Parse(split[0]);
                decimal denominator = decimal.Parse(split[1]);
                frameRate = numerator / denominator;
                Trace.TraceInformation("Framerate: " + frameRate);
            }
            NewFFProcess();
            var dirInfo = new DirectoryInfo(dir);
            var files = dirInfo.EnumerateFiles(searchPattern);
            var sb = new StringBuilder();
            foreach (var file in files)
            {
                sb.Append(file.FullName);

                if (!skipFrameRateCalc)
                {
                    _FFProcess.StartProbe($"-v 0 -of csv=p=0 -select_streams v:0 -show_entries stream=r_frame_rate \"{file.FullName}\"");
                    string output = _FFProcess.StandardOutput.ReadToEnd().Trim();
                    frameRate = decimal.Parse(output.Split(@"/")[0]) / decimal.Parse(output.Split(@"/")[1]);

                    Trace.TraceInformation("Framerate: " + frameRate);
                    _FFProcess.WaitForExit();

                    Trace.TraceInformation("Process Exit Code: " + _FFProcess.ExitCode);
                }

                _FFProcess.StartProbe($"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{file.FullName}\"");
                decimal totalSeconds = decimal.Parse(_FFProcess.StandardOutput.ReadToEnd().Trim());

                Trace.TraceInformation("Total Seconds: " + totalSeconds);
                _FFProcess.WaitForExit();

                Trace.TraceInformation("Process Exit Code: " + _FFProcess.ExitCode);

                decimal totalFrames = frameRate * totalSeconds;
                _FilesDict.TryAdd(file.FullName, (int)totalFrames); //rounds down
                _TotalDirFrames += (int)totalFrames;
                sb.Append(" -- " + (int)totalFrames + " -- " + totalFrames + Environment.NewLine);
            }
            _FFProcess.Dispose();
            return sb.ToString();
        }
        public string GetFrameCountFromPackets(string dir, string searchPattern)
        {
            NewFFProcess();
            var dirInfo = new DirectoryInfo(dir);
            var files = dirInfo.EnumerateFiles(searchPattern);
            var sb = new StringBuilder();
            foreach (var file in files)
            {
                sb.Append(file.FullName);
                _FFProcess.StartInfo.Arguments = $"-v error -select_streams v:0 -count_packets -show_entries stream=nb_read_packets \"{file.FullName}\"";
                _FFProcess.Start();
                int totalFrames = int.Parse(_FFProcess.StandardOutput.ReadToEnd().Split("=")[1].Split("[")[0].Trim());
                _FFProcess.WaitForExit();
                _FilesDict.TryAdd(file.FullName, totalFrames);
                sb.Append(" -- " + totalFrames + Environment.NewLine);
            }
            _FFProcess.Dispose();
            return sb.ToString();
        }
        public string GetFrameCount(string dir, string searchPattern)
        {
            NewFFProcess();
            var dirInfo = new DirectoryInfo(dir);
            var files = dirInfo.EnumerateFiles(searchPattern);
            var sb = new StringBuilder();
            foreach (var file in files)
            {
                sb.Append(file.FullName);
                _FFProcess.StartInfo.Arguments = $"-v error -select_streams v:0 -count_frames -show_entries stream=nb_read_frames \"{file.FullName}\"";
                _FFProcess.Start();
                int totalFrames = int.Parse(_FFProcess.StandardOutput.ReadToEnd().Split("=")[1].Split("[")[0].Trim());
                _FFProcess.WaitForExit();
                _FilesDict.TryAdd(file.FullName, totalFrames);
                sb.Append(" -- " + totalFrames + Environment.NewLine);
            }
            _FFProcess.Dispose();
            return sb.ToString();
        }
        public async Task<(int, string)> RunProfile(string args, string outputDir, string ext, IProgress<double> progress, MainWindowViewModel viewModel)
        {
            //Start out having progress bar show prog of entire dir
            //Progress would be current progress plus the sum of the files already done
            _ViewModel = viewModel;
            _UIProgress = progress;
            //NewFFProcess("ffmpeg");
            //Trace.TraceInformation(_FFProcess.StartInfo.FileName);
            //_FFProcess.OutputDataReceived += new DataReceivedEventHandler(StdOutHandler);
            Trace.TraceInformation($"Starting transcode to {outputDir}");
            foreach (var filePath in _FilesDict.Keys)
            {
                NewFFProcess();
                _FFProcess.OutputDataReceived += new DataReceivedEventHandler(StdOutHandler);
                //Path.GetFileName(filePath);
                if (CancelQ)
                {
                    _FFProcess.Dispose();
                    CancelQ = false;
                    Trace.TraceInformation($"Canceled on {filePath}");
                    return (-1, filePath);
                }
                _FFProcess.StartMpeg($"-i \"{filePath}\" -progress pipe:1 {args} \"{Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filePath) + ext)}\"");
                _FFProcess.BeginOutputReadLine();
                await ReadStdErr();
                await _FFProcess.WaitForExitAsync();
                Trace.TraceInformation("Process Exit Code: " + _FFProcess.ExitCode);
                if (CancelQ)
                {
                    _FFProcess.Dispose();
                    CancelQ = false;
                    Trace.TraceInformation($"Canceled on {filePath}");
                    return (-1, filePath);
                }
                if (_SkipFile)
                {
                    _SkipFile = false;
                }
                else if (_FFProcess.ExitCode != 0)
                {
                    int exitCode = _FFProcess.ExitCode;
                    _FFProcess.Dispose();
                    Trace.TraceInformation($"Exited with code {exitCode} on {filePath}");
                    return (exitCode, _LastStdErrLine);
                }
                _TotalPrevFrameProgress += _FilesDict[filePath];
                lock (_DisposeLock)
                {
                    _FFProcess.Dispose();
                }
                Trace.TraceInformation($"File transcode, \"{filePath}\", complete");
            }
            //_FFProcess.Dispose();
            return (0, String.Empty);
        }
        public string Trim(string startTime, string endTime, string inputFile, string outputFile)
        {
            throw new NotImplementedException();
        }
        public async Task<(int, string)> TrimDir(ObservableCollection<TrimData> trimData, string sourceDir, string outputDir, IProgress<double> progress, ListViewData item, MainWindowViewModel viewModel)
        {
            _UIProgress = progress;
            _ViewModel = viewModel;
            var trimDataValidTimeCodes = trimData.Where(x => x.StartTime is not null && x.EndTime is not null);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                item.Description.FileCount = trimDataValidTimeCodes.Count();
                item.Label = $"{item.Name} ({item.Description.CurrentFileNumber}/{item.Description.FileCount})";
            });
            Trace.TraceInformation($"Starting trim to {outputDir} from {sourceDir}");
            if (outputDir == string.Empty || sourceDir == outputDir)
            {
                foreach (TrimData data in trimDataValidTimeCodes)
                {
                    _EndTime = (double)data.EndTime!.Value * 1000;
                    NewFFProcess();
                    _FFProcess.OutputDataReceived += new DataReceivedEventHandler(TrimStdOutHandler);
                    if (CancelQ)
                    {
                        _FFProcess.Dispose();
                        CancelQ = false;
                        Trace.TraceInformation($"Canceled on {data.FileInfo.FullName}");
                        return (-1, data.FileInfo.FullName);
                    }
                    string newFile = Path.Combine(data.FileInfo.Directory.FullName, $"_{data.FileInfo.Name}");
                    _FFProcess.StartMpeg($"-progress pipe:1 -ss {data.StartTime!.FormattedString} -to {data.EndTime.FormattedString} -i \"{data.FileInfo.FullName}\" -map 0 -codec copy \"{newFile}\"");
                    _FFProcess.BeginOutputReadLine();
                    await ReadStdErr();
                    await _FFProcess.WaitForExitAsync();
                    Trace.TraceInformation("Process Exit Code: " + _FFProcess.ExitCode);
                    if (CancelQ)
                    {
                        _FFProcess.Dispose();
                        CancelQ = false;
                        Trace.TraceInformation($"Canceled on {data.FileInfo.FullName}");
                        return (-1, data.FileInfo.FullName);
                    }
                    if (_SkipFile)
                    {
                        _SkipFile = false;
                    }
                    else if (_FFProcess.ExitCode == 0)
                    {
                        string rename = data.FileInfo.FullName;
                        data.FileInfo.Delete();
                        File.Move(newFile, rename);
                    }
                    else
                    {
                        int exitCode = _FFProcess.ExitCode;
                        lock (_DisposeLock)
                        {
                        _FFProcess.Dispose();
                        }
                        Trace.TraceInformation($"Exited with code {exitCode} on {data.FileInfo.FullName}");
                        return (exitCode, _LastStdErrLine);
                    }
                    lock (_DisposeLock)
                    {
                        _FFProcess.Dispose();
                    }
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        item.Label = $"{item.Name} ({++item.Description.CurrentFileNumber}/{item.Description.FileCount})";
                    });
                    Trace.TraceInformation($"File trim, \"{data.FileInfo.FullName}\", complete");
                }
                return (0, String.Empty);
            }
            else
            {
                foreach (TrimData data in trimDataValidTimeCodes)
                {
                    _EndTime = (double)data.EndTime!.Value * 1000;
                    NewFFProcess();
                    _FFProcess.OutputDataReceived += new DataReceivedEventHandler(TrimStdOutHandler);
                    if (CancelQ)
                    {
                        _FFProcess.Dispose();
                        CancelQ = false;
                        Trace.TraceInformation($"Canceled on {data.FileInfo.FullName}");
                        return (-1, data.FileInfo.FullName);
                    }
                    string newFile = Path.Combine(outputDir, data.FileInfo.Name);
                    _FFProcess.StartMpeg($"-progress pipe:1 -ss {data.StartTime!.FormattedString} -to {data.EndTime.FormattedString} -i \"{data.FileInfo.FullName}\" -map 0 -codec copy \"{newFile}\"");
                    _FFProcess.BeginOutputReadLine();
                    await ReadStdErr();
                    await _FFProcess.WaitForExitAsync();
                    Trace.TraceInformation("Process Exit Code: " + _FFProcess.ExitCode);
                    if (CancelQ)
                    {
                        _FFProcess.Dispose();
                        CancelQ = false;
                        Trace.TraceInformation($"Canceled on {data.FileInfo.FullName}");
                        return (-1, data.FileInfo.FullName);
                    }
                    if (_SkipFile)
                    {
                        _SkipFile = false;
                    }
                    else if (_FFProcess.ExitCode != 0)
                    {
                        int exitCode = _FFProcess.ExitCode;
                        lock (_DisposeLock)
                        {
                            _FFProcess.Dispose(); 
                        }
                        Trace.TraceInformation($"Exited with code {exitCode} on {data.FileInfo.FullName}");
                        return (exitCode, _LastStdErrLine);
                    }
                    lock (_DisposeLock)
                    {
                        _FFProcess.Dispose();
                    }
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        item.Label = $"{item.Name} ({++item.Description.CurrentFileNumber}/{item.Description.FileCount})";
                    });
                    Trace.TraceInformation($"File trim, \"{data.FileInfo.FullName}\", complete");
                }
                return (0, String.Empty);
            }
        }
        public void Stop()
        {
            CancelQ = true;
            lock (_DisposeLock)
            {
                try
                {
                    _FFProcess.Refresh();
                    if (!_FFProcess.HasExited)
                    {
                        _FFProcess.StandardInput.WriteLine("q");
                    }
                }
                catch (ObjectDisposedException)
                {
                    Trace.TraceInformation("FF Process Object Disposed Exception");
                    return;
                }
                catch (InvalidOperationException)
                {
                    Trace.TraceInformation("FF Process Invalid Operation Exception");
                    return;
                }
            }
        }
        public static bool CheckFFmpegExecutable(string location)
        {
            string ffprobe;
            string ffmpeg;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                ffprobe = "ffprobe";
                ffmpeg = "ffmpeg";
            }
            else
            {
                ffprobe = "ffprobe.exe";
                ffmpeg = "ffmpeg.exe";
            }
            if (File.Exists(Path.Combine(location, ffmpeg)) && File.Exists(Path.Combine(location, ffprobe)))
            {
                var proc = new Process
                {
                    StartInfo = DefaultStartInfo()
                };
                proc.StartInfo.FileName = Path.Combine(location, ffmpeg); //only runs ffmpeg not ffprobe
                proc.Start();
                var stdout = proc.StandardOutput.ReadToEnd();
                var stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit(1000);
                if (!proc.HasExited)
                {
                    proc.Close();
                }
                proc.Dispose();
                if (string.IsNullOrEmpty(stdout) && string.IsNullOrEmpty(stderr))
                {
                    return false;
                }
                if (stderr.StartsWith("ffprobe") || stderr.StartsWith("ffmpeg"))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
        private async Task ReadStdErr()
        {
            var sr = _FFProcess.StandardError;
            var sb = new StringBuilder();
            while (!sr.EndOfStream)
            {
                var inputChar = (char)sr.Read();
                sb.Append(inputChar);
                if (sb.EndsWith(Environment.NewLine))
                {
                    _LastStdErrLine = sb.ToStringTrimEnd(Environment.NewLine);
                    sb = new StringBuilder();
                    Trace.TraceInformation(_LastStdErrLine);
                }
                else if (sb.Contains("[y/N]", StringComparison.OrdinalIgnoreCase))
                {
                    Trace.TraceInformation("Yes/No prompt found");
                    var line = sb.ToString();
                    if (_ViewModel!.AutoOverwriteCheck)
                    {
                        await _FFProcess.StandardInput.WriteLineAsync("y");
                    }
                    else
                    {
                        await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            var msgBox = AvaloniaMessageBox.MessageBox.GetMessageBox(new AvaloniaMessageBox.MessageBoxParams
                            {
                                Buttons = AvaloniaMessageBox.MessageBoxButtons.YesNo,
                                Title = "FFmpeg yes/no prompt",
                                Message = line.Replace("[y/N]", "").Trim()
                            });
                            var app = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
                            var result = await msgBox.ShowDialog(app.MainWindow);
                            if (result == AvaloniaMessageBox.MessageBoxResult.Yes)
                            {
                                await _FFProcess.StandardInput.WriteLineAsync("y");
                            }
                            else
                            {
                                _SkipFile = true;
                                await _FFProcess.StandardInput.WriteLineAsync("n");
                            }
                        }, DispatcherPriority.MaxValue);
                    }
                    sb = new StringBuilder();
                    Trace.TraceInformation(line);
                }
            }
        }
        private void StdOutHandler(object sendingProcess, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                Trace.TraceInformation("STDOUT**--" + e.Data);
                if (e.Data.Contains("frame="))
                {
                    _LastFrame = int.Parse(e.Data.Split("=")[1].Trim());
                    Trace.TraceInformation($"Progress: {_LastFrame} frames finished (file)");
                    double progress = ((double)_LastFrame + _TotalPrevFrameProgress) / _TotalDirFrames;
                    Trace.TraceInformation($"Progress: {progress} percentage frames finished (directory)");
                    _UIProgress!.Report(((double)_LastFrame + _TotalPrevFrameProgress) / _TotalDirFrames);
                }
            }
        }
        private void TrimStdOutHandler(object sendingProcess, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                Trace.TraceInformation("STDOUT**--" + e.Data);
                if (e.Data.Contains("out_time="))
                {
                    double currentTime = double.Parse(e.Data.Split("=")[1].Replace(":", "").Replace(".", ""));
                    Trace.TraceInformation($"Progress: {currentTime} (current time) / {_EndTime} (end time)");
                    _UIProgress!.Report(currentTime / _EndTime);
                }
                else if (e.Data.Contains("progress=end"))
                {
                    Trace.TraceInformation("File completed");
                    _UIProgress!.Report(1);
                }
            }
        }
    }
}