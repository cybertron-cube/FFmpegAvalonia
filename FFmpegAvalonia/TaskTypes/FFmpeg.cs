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
        private bool _audioFile = false;
        private FFmpegProcess _ffProcess;
        private readonly string _ffMpegPath;
        private readonly ConcurrentDictionary<string, int> _filesDict;
        private int _lastFrame;
        private long _queueCanceled = 0;
        public bool QueueCanceled
        {
            get => Interlocked.Read(ref _queueCanceled) == 1;
            private set => Interlocked.Exchange(ref _queueCanceled, Convert.ToInt64(value));
        }
        private int _totalPrevFrameProgress;
        private int _totalDirFrames;
        private double _endTime;
        private IProgress<double>? _uIProgress;
        private readonly object _disposeLock = new();
        private string _lastStdErrLine = String.Empty;
        private MainWindowViewModel? _viewModel;
        private bool _skipFile;
        public FFmpeg(string ffmpegdir)
        {
            _ffMpegPath = ffmpegdir;
            _ffProcess = new FFmpegProcess(_ffMpegPath)
            {
                StartInfo = DefaultStartInfo(),
                EnableRaisingEvents = true,
            };
            _filesDict = new ConcurrentDictionary<string, int>();
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
        private void NewFFProcess(bool detachProcess = false)
        {
            if (detachProcess)
            {
                _ffProcess = new FFmpegProcess(_ffMpegPath)
                {
                    StartInfo = new(),
                    EnableRaisingEvents = true
                };
            }
            else
            {
                _ffProcess = new FFmpegProcess(_ffMpegPath)
                {
                    StartInfo = DefaultStartInfo(),
                    EnableRaisingEvents = true
                };
            }
        }
        public string SetProgression(string dir, string ext, string args)
        {
            if (HashMaps.FileFormats.TryGetValue(ext.ToLower(), out string? type) && type == "audio")
            {
                _audioFile = true;
                return GetDuration(dir, '*' + ext);
            }
            else
            {
                return GetFrameCountApproximate(dir, '*' + ext, args);
            }
        }
        public string GetDuration(string dir, string searchPattern)
        {
            NewFFProcess();
            var dirInfo = new DirectoryInfo(dir);
            var files = dirInfo.EnumerateFiles(searchPattern);
            var sb = new StringBuilder();
            foreach (var file in files)
            {
                sb.Append(file.FullName);

                _ffProcess.StartProbe($"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{file.FullName}\"");
                decimal totalSeconds = decimal.Parse(_ffProcess.StandardOutput.ReadToEnd().Trim());
                int totalSecondsRound = Convert.ToInt32(totalSeconds);

                Trace.TraceInformation("Total Seconds: " + totalSeconds);
                _ffProcess.WaitForExit();

                Trace.TraceInformation("Process Exit Code: " + _ffProcess.ExitCode);

                _filesDict.TryAdd(file.FullName, totalSecondsRound);
                sb.Append(" -- " + totalSecondsRound + Environment.NewLine);
            }
            _ffProcess.Dispose();
            return sb.ToString();
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
                    _ffProcess.StartProbe($"-v 0 -of csv=p=0 -select_streams v:0 -show_entries stream=r_frame_rate \"{file.FullName}\"");
                    string output = _ffProcess.StandardOutput.ReadToEnd().Trim();
                    frameRate = decimal.Parse(output.Split(@"/")[0]) / decimal.Parse(output.Split(@"/")[1]);

                    Trace.TraceInformation("Framerate: " + frameRate);
                    _ffProcess.WaitForExit();

                    Trace.TraceInformation("Process Exit Code: " + _ffProcess.ExitCode);
                }

                _ffProcess.StartProbe($"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{file.FullName}\"");
                decimal totalSeconds = decimal.Parse(_ffProcess.StandardOutput.ReadToEnd().Trim());

                Trace.TraceInformation("Total Seconds: " + totalSeconds);
                _ffProcess.WaitForExit();

                Trace.TraceInformation("Process Exit Code: " + _ffProcess.ExitCode);

                decimal totalFrames = frameRate * totalSeconds;
                _filesDict.TryAdd(file.FullName, (int)totalFrames); //rounds down
                _totalDirFrames += (int)totalFrames;
                sb.Append(" -- " + (int)totalFrames + " -- " + totalFrames + Environment.NewLine);
            }
            _ffProcess.Dispose();
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
                _ffProcess.StartInfo.Arguments = $"-v error -select_streams v:0 -count_packets -show_entries stream=nb_read_packets \"{file.FullName}\"";
                _ffProcess.Start();
                int totalFrames = int.Parse(_ffProcess.StandardOutput.ReadToEnd().Split("=")[1].Split("[")[0].Trim());
                _ffProcess.WaitForExit();
                _filesDict.TryAdd(file.FullName, totalFrames);
                sb.Append(" -- " + totalFrames + Environment.NewLine);
            }
            _ffProcess.Dispose();
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
                _ffProcess.StartInfo.Arguments = $"-v error -select_streams v:0 -count_frames -show_entries stream=nb_read_frames \"{file.FullName}\"";
                _ffProcess.Start();
                int totalFrames = int.Parse(_ffProcess.StandardOutput.ReadToEnd().Split("=")[1].Split("[")[0].Trim());
                _ffProcess.WaitForExit();
                _filesDict.TryAdd(file.FullName, totalFrames);
                sb.Append(" -- " + totalFrames + Environment.NewLine);
            }
            _ffProcess.Dispose();
            return sb.ToString();
        }
        public async Task<(int, string)> RunProfile(string args, string outputDir, string ext, IProgress<double> progress, MainWindowViewModel viewModel, CancellationToken ct, bool detachProcess = false)
        {
            //Start out having progress bar show prog of entire dir
            //Progress would be current progress plus the sum of the files already done
            _viewModel = viewModel;
            _uIProgress = progress;
            Trace.TraceInformation($"Starting transcode to {outputDir}");
            foreach (var filePath in _filesDict.Keys)
            {
                if (detachProcess)
                    Trace.TraceInformation("Creating detached ffmpeg process");
                else
                    Trace.TraceInformation("Creating attached ffmpeg process");
                NewFFProcess(detachProcess);
                if (!detachProcess)
                {
                    if (_audioFile)
                    {
                        _ffProcess.OutputDataReceived += new DataReceivedEventHandler(StdOutHandlerTimeProg);
                        _endTime = _filesDict[filePath];
                    }
                    else
                    {
                        _ffProcess.OutputDataReceived += new DataReceivedEventHandler(StdOutHandlerFrameProg);
                    }
                }
                if (ct.IsCancellationRequested)
                {
                    if (detachProcess)
                    {
                        _ffProcess.Kill();
                        await _ffProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    _ffProcess.Dispose();
                    Trace.TraceInformation($"Canceled on {filePath}");
                    return (-1, filePath);
                }
                _ffProcess.StartMpeg($"-i \"{filePath}\" -progress pipe:1 {args} \"{Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filePath) + ext)}\"");
                if (detachProcess)
                {
                    try
                    {
                        await _ffProcess.WaitForExitAsync(ct);
                    }
                    catch (TaskCanceledException)
                    {
                        _ffProcess.Kill();
                        await _ffProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    }
                }
                else
                {
                    _ffProcess.BeginOutputReadLine();
                    try
                    {
                        await ReadStdErr(ct); 
                    }
                    catch (TaskCanceledException taskCanceledExc)
                    {
                        QueueCanceled = true;
                        await _ffProcess.StandardInput.WriteAsync('q');
                        try
                        {
                            await _ffProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); 
                        }
                        catch (TimeoutException timeOutExc)
                        {
                            _ffProcess.Kill();
                            await _ffProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                            _ffProcess.Dispose();
                            Trace.TraceError(timeOutExc.ToString());
                            return (-32, timeOutExc.ToString());
                        }
                        catch (Exception excInner)
                        {
                            _ffProcess.Kill();
                            await _ffProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                            _ffProcess.Dispose();
                            Trace.TraceError(excInner.ToString());
                            return (-33, excInner.ToString());
                        }
                        Trace.TraceInformation(taskCanceledExc.ToString());
                    }
                    catch (Exception exc)
                    {
                        _ffProcess.Kill();
                        _ffProcess.Dispose();
                        Trace.TraceError(exc.ToString());
                        return (-31, exc.ToString());
                    }
                }
                Trace.TraceInformation("Process Exit Code: " + _ffProcess.ExitCode);
                if (ct.IsCancellationRequested)
                {
                    if (detachProcess)
                    {
                        _ffProcess.Kill();
                        await _ffProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    _ffProcess.Dispose();
                    Trace.TraceInformation($"Canceled on {filePath}");
                    return (-1, filePath);
                }
                if (_skipFile)
                {
                    _skipFile = false;
                }
                else if (_ffProcess.ExitCode != 0)
                {
                    int exitCode = _ffProcess.ExitCode;
                    _ffProcess.Dispose();
                    Trace.TraceInformation($"Exited with code {exitCode} on {filePath}");
                    return (exitCode, _lastStdErrLine);
                }
                _totalPrevFrameProgress += _filesDict[filePath];
                lock (_disposeLock)
                {
                    _ffProcess.Dispose();
                }
                Trace.TraceInformation($"File transcode, \"{filePath}\", complete");
            }
            if (detachProcess)
            {
                _uIProgress.Report(1);
            }
            return (0, String.Empty);
        }
        public string Trim(string startTime, string endTime, string inputFile, string outputFile)
        {
            throw new NotImplementedException();
        }
        public async Task<(int, string)> TrimDir(ObservableCollection<TrimData> trimData, string sourceDir, string outputDir, IProgress<double> progress, ListViewData item, MainWindowViewModel viewModel, CancellationToken ct, bool detachProcess = false)
        {
            _uIProgress = progress;
            _viewModel = viewModel;
            bool overwrite = outputDir == String.Empty || sourceDir == outputDir;
            var trimDataValidTimeCodes = trimData.Where(x => x.StartTime is not null && x.EndTime is not null);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                item.Description.FileCount = trimDataValidTimeCodes.Count();
                item.Label = $"{item.Name} ({item.Description.CurrentFileNumber}/{item.Description.FileCount})";
            });
            Trace.TraceInformation($"Starting trim to {outputDir} from {sourceDir}");
            foreach (TrimData data in trimDataValidTimeCodes)
            {
                _endTime = data.EndTime!.GetTotalSeconds();
                if (detachProcess)
                    Trace.TraceInformation("Creating detached ffmpeg process");
                else
                    Trace.TraceInformation("Creating attached ffmpeg process");
                NewFFProcess(detachProcess);
                if (!detachProcess)
                {
                    _ffProcess.OutputDataReceived += new DataReceivedEventHandler(StdOutHandlerTimeProg); 
                }
                if (ct.IsCancellationRequested)
                {
                    if (detachProcess)
                    {
                        _ffProcess.Kill();
                        await _ffProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    _ffProcess.Dispose();
                    Trace.TraceInformation($"Canceled on {data.FileInfo.FullName}");
                    return (-1, data.FileInfo.FullName);
                }
                string newFile;
                if (overwrite)
                {
                    newFile = Path.Combine(data.FileInfo.Directory.FullName, $"_{data.FileInfo.Name}");
                }
                else
                {
                    newFile = Path.Combine(outputDir, data.FileInfo.Name);
                }
                _ffProcess.StartMpeg($"-progress pipe:1 -ss {data.StartTime!.FormattedString} -to {data.EndTime.FormattedString} -i \"{data.FileInfo.FullName}\" -map 0 -codec copy \"{newFile}\"");
                if (detachProcess)
                {
                    try
                    {
                        await _ffProcess.WaitForExitAsync(ct);
                    }
                    catch (TaskCanceledException)
                    {
                        _ffProcess.Kill();
                        await _ffProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    }
                }
                else
                {
                    _ffProcess.BeginOutputReadLine();
                    try
                    {
                        await ReadStdErr(ct); 
                    }
                    catch (TaskCanceledException taskCanceledExc)
                    {
                        QueueCanceled = true;
                        await _ffProcess.StandardInput.WriteAsync('q');
                        try
                        {
                            await _ffProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                        }
                        catch (TimeoutException timeOutExc)
                        {
                            _ffProcess.Kill();
                            await _ffProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                            _ffProcess.Dispose();
                            Trace.TraceError(timeOutExc.ToString());
                            return (-32, timeOutExc.ToString());
                        }
                        catch (Exception excInner)
                        {
                            _ffProcess.Kill();
                            await _ffProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                            _ffProcess.Dispose();
                            Trace.TraceError(excInner.ToString());
                            return (-33, excInner.ToString());
                        }
                        Trace.TraceInformation(taskCanceledExc.ToString());
                    }
                    catch (Exception exc)
                    {
                        _ffProcess.Kill();
                        _ffProcess.Dispose();
                        Trace.TraceError(exc.ToString());
                        return (-31, exc.ToString());
                    }
                }
                Trace.TraceInformation("Process Exit Code: " + _ffProcess.ExitCode);
                if (ct.IsCancellationRequested)
                {
                    if (detachProcess)
                    {
                        _ffProcess.Kill();
                        await _ffProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    _ffProcess.Dispose();
                    Trace.TraceInformation($"Canceled on {data.FileInfo.FullName}");
                    return (-1, data.FileInfo.FullName);
                }
                if (_skipFile)
                {
                    _skipFile = false;
                }
                else if (_ffProcess.ExitCode == 0 && overwrite)
                {
                    string rename = data.FileInfo.FullName;
                    data.FileInfo.Delete();
                    File.Move(newFile, rename);
                }
                else if (_ffProcess.ExitCode != 0)
                {
                    int exitCode = _ffProcess.ExitCode;
                    lock (_disposeLock)
                    {
                        _ffProcess.Dispose();
                    }
                    Trace.TraceInformation($"Exited with code {exitCode} on {data.FileInfo.FullName}");
                    return (exitCode, _lastStdErrLine);
                }
                lock (_disposeLock)
                {
                    _ffProcess.Dispose();
                }
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    item.Label = $"{item.Name} ({++item.Description.CurrentFileNumber}/{item.Description.FileCount})";
                });
                Trace.TraceInformation($"File trim, \"{data.FileInfo.FullName}\", complete");
            }
            return (0, String.Empty);
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
        private async Task ReadStdErr(CancellationToken ct)
        {
            var sr = _ffProcess.StandardError;
            var sb = new StringBuilder();
            char[] buffer = new char[1];
            while ((await sr.ReadAsync(buffer, 0, 1).WaitAsync(ct)) > 0)
            {
                sb.Append(buffer[0]);
                if (buffer[0] == '\n')
                {
                    _lastStdErrLine = sb.ToStringTrimEnd(Environment.NewLine);
                    sb.Clear();
                    Trace.TraceInformation("STDERR**--" + _lastStdErrLine);
                }
                else if (buffer[0] == ']' && sb[^2] == 'N' && sb[^3] == '/' && sb[^4] == 'y' && sb[^5] == '[') //only N] should be necessary to search for but just in case we search for the whole thing
                {
                    Trace.TraceInformation("Yes/No prompt found");
                    await sr.ReadAsync(buffer, 0, 1); //This should be space character ' '
                    Debug.WriteLine(buffer[0]);
                    _lastStdErrLine = sb.ToString();
                    if (_viewModel!.AutoOverwriteCheck)
                    {
                        await _ffProcess.StandardInput.WriteLineAsync("y");
                    }
                    else
                    {
                        await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            var msgBox = AvaloniaMessageBox.MessageBox.GetMessageBox(new AvaloniaMessageBox.MessageBoxParams
                            {
                                Buttons = AvaloniaMessageBox.MessageBoxButtons.YesNo,
                                Title = "FFmpeg yes/no prompt",
                                Message = _lastStdErrLine.Replace("[y/N]", "").Trim()
                            });
                            var app = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
                            var result = await msgBox.ShowDialog(app.MainWindow);
                            if (result == AvaloniaMessageBox.MessageBoxResult.Yes)
                            {
                                await _ffProcess.StandardInput.WriteLineAsync("y");
                            }
                            else
                            {
                                _skipFile = true;
                                await _ffProcess.StandardInput.WriteLineAsync("n");
                            }
                        }, DispatcherPriority.MaxValue);
                    }
                    sb.Clear();
                    Trace.TraceInformation("STDERR**--" + _lastStdErrLine);
                }
            }
        }
        private void StdOutHandlerFrameProg(object sendingProcess, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                Trace.TraceInformation("STDOUT**--" + e.Data);
                if (e.Data.Contains("frame="))
                {
                    _lastFrame = int.Parse(e.Data.Split("=")[1].Trim());
                    Trace.TraceInformation($"Progress: {_lastFrame} frames finished (file)");
                    double progress = ((double)_lastFrame + _totalPrevFrameProgress) / _totalDirFrames;
                    Trace.TraceInformation($"Progress: {progress} percentage frames finished (directory)");
                    _uIProgress!.Report(((double)_lastFrame + _totalPrevFrameProgress) / _totalDirFrames);
                }
                else if (e.Data.Contains("progress=end") && !QueueCanceled)
                {
                    Trace.TraceInformation("File completed");
                    _uIProgress!.Report(1);
                }
            }
        }
        private void StdOutHandlerTimeProg(object sendingProcess, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                Trace.TraceInformation("STDOUT**--" + e.Data);
                if (e.Data.Contains("out_time="))
                {
                    double currentTime = TimeCode.GetTotalSeconds(e.Data.Split('=')[1].Replace("-", ""));
                    Trace.TraceInformation($"Progress: {currentTime} (current time) / {_endTime} (end time)");
                    _uIProgress!.Report(currentTime / _endTime);
                }
                else if (e.Data.Contains("progress=end") && !QueueCanceled)
                {
                    Trace.TraceInformation("File completed");
                    _uIProgress!.Report(1);
                }
            }
        }
    }
}