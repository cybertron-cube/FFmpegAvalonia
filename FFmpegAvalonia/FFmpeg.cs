using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Extensions.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using MessageBox.Avalonia.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ExtensionMethods;

namespace FFmpegAvalonia
{
    class FFmpeg
    {
        private FFmpegProcess _Process;
        private readonly string _FFmpegPath;
        private readonly bool _IsLinux;
        private readonly ConcurrentDictionary<string, int> _FilesDict; //maybe not need to be concurrentdict
        private readonly object _CancelQLock = new();
        private int _LastFrame;
        private bool _CancelQ;
        private int _TotalPrevFrameProgress;
        private int _TotalDirFrames;
        private IProgress<double>? _UIProgress;
        private bool _IsDisposed;
        private readonly object _IsDisposedLock = new();
        private ViewModel? _ViewModel;
        public FFmpeg(string ffmpegdir)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _IsLinux = true;
            }
            _FFmpegPath = ffmpegdir;
            _Process = new FFmpegProcess
            {
                StartInfo = DefaultStartInfo(),
                EnableRaisingEvents = true,
            };
            _FilesDict = new ConcurrentDictionary<string, int>();
            //_CancelQ = false;
            //_IsDisposed = false;
            //_Process.Disposed += new EventHandler(Process_Disposed);
        }
        private class FFmpegProcess : Process
        {
            public void CleanStop(FFmpeg ff)
            {
                lock (ff._IsDisposedLock)
                {
                    ff._IsDisposed = true;
                    this.Dispose();
                }
            }
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
        private void SetProcExeOS(string ffExeName)
        {
            if (_IsLinux)
            {
                _Process.StartInfo.FileName = Path.Combine(_FFmpegPath, ffExeName);
            }
            else
            {
                _Process.StartInfo.FileName = Path.Combine(_FFmpegPath, ffExeName + ".exe");
            }
        }
        private void NewFFProcess()
        {
            lock (_IsDisposedLock)
            {
                if (_IsDisposed)  //(_Process.StartInfo != DefaultStartInfo()) //does not work as intended
                {
                    _Process = new FFmpegProcess
                    {
                        StartInfo = DefaultStartInfo(),
                        EnableRaisingEvents = true
                    };
                    _IsDisposed = false;
                    //_Process.Disposed += new EventHandler(Process_Disposed);  //Process_Disposed not firing???????
                }
            }
        }
        private void NewFFProcess(string ffExeName)
        {
            NewFFProcess();
            SetProcExeOS(ffExeName);
        }
        public string GetFrameCountApproximate(string dir, string searchPattern)
        {
            NewFFProcess("ffprobe");
            Trace.TraceInformation(_Process.StartInfo.FileName);
            var dirInfo = new DirectoryInfo(dir);
            var files = dirInfo.EnumerateFiles(searchPattern);
            var sb = new StringBuilder();
            foreach ( var file in files)
            {
                sb.Append(file.FullName);
                _Process.StartInfo.Arguments = $"-v 0 -of csv=p=0 -select_streams v:0 -show_entries stream=r_frame_rate \"{file.FullName}\"";
                _Process.Start();
                Trace.TraceInformation("Process ID: " + _Process.Id);
                string output = _Process.StandardOutput.ReadToEnd().Trim();
                decimal frameRate = Decimal.Parse(output.Split(@"/")[0]) / Decimal.Parse(output.Split(@"/")[1]);

                Trace.TraceInformation("Framerate: " + frameRate);
                _Process.WaitForExit();

                Trace.TraceInformation("Process Exit Code: " + _Process.ExitCode);

                _Process.StartInfo.Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{file.FullName}\"";
                _Process.Start();
                Trace.TraceInformation("Process ID: " + _Process.Id);
                decimal totalSeconds = Decimal.Parse(_Process.StandardOutput.ReadToEnd().Trim());

                Trace.TraceInformation("Total Seconds: " + totalSeconds);
                _Process.WaitForExit();

                Trace.TraceInformation("Process Exit Code: " + _Process.ExitCode);

                decimal totalFrames = frameRate * totalSeconds;
                _FilesDict.TryAdd(file.FullName, (int)totalFrames); //rounds down
                _TotalDirFrames += (int)totalFrames;
                sb.Append(" -- " + (int)totalFrames + " -- " + totalFrames + System.Environment.NewLine);
            }
            _Process.CleanStop(this);
            return sb.ToString();
        }
        public string GetFrameCountFromPackets(string dir, string searchPattern) 
        {
            NewFFProcess("ffprobe");
            var dirInfo = new DirectoryInfo(dir);
            var files = dirInfo.EnumerateFiles(searchPattern);
            var sb = new StringBuilder();
            foreach (var file in files)
            {
                sb.Append(file.FullName);
                _Process.StartInfo.Arguments = $"-v error -select_streams v:0 -count_packets -show_entries stream=nb_read_packets \"{file.FullName}\"";
                _Process.Start();
                int totalFrames = Int32.Parse(_Process.StandardOutput.ReadToEnd().Split("=")[1].Split("[")[0].Trim());
                _Process.WaitForExit();
                _FilesDict.TryAdd(file.FullName, totalFrames);
                sb.Append(" -- " + totalFrames + System.Environment.NewLine);
            }
            _Process.CleanStop(this);
            return sb.ToString();
        }
        public string GetFrameCount(string dir, string searchPattern)
        {
            NewFFProcess("ffprobe");
            var dirInfo = new DirectoryInfo(dir);
            var files = dirInfo.EnumerateFiles(searchPattern);
            var sb = new StringBuilder();
            foreach (var file in files)
            {
                sb.Append(file.FullName);
                _Process.StartInfo.Arguments = $"-v error -select_streams v:0 -count_frames -show_entries stream=nb_read_frames \"{file.FullName}\"";
                _Process.Start();
                int totalFrames = Int32.Parse(_Process.StandardOutput.ReadToEnd().Split("=")[1].Split("[")[0].Trim());
                _Process.WaitForExit();
                _FilesDict.TryAdd(file.FullName, totalFrames);
                sb.Append(" -- " + totalFrames + System.Environment.NewLine);
            }
            _Process.CleanStop(this);
            return sb.ToString();
        }
        public async Task<string> RunProfile(string args, string outputDir, string ext, IProgress<double> progress, ViewModel viewModel)
        {
            //Start out having progress bar show prog of entire dir
            //Progress would be current progress plus the sum of the files already done
            _ViewModel = viewModel;
            _UIProgress = progress;
            NewFFProcess("ffmpeg");
            Trace.TraceInformation(_Process.StartInfo.FileName);
            _Process.OutputDataReceived += new DataReceivedEventHandler(StdOutHandler);
            foreach (var filePath in _FilesDict.Keys)
            {
                //Path.GetFileName(filePath);
                Trace.TraceInformation($"-i \"{filePath}\" -progress pipe:1 {args} \"{Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filePath) + ext)}\"");
                _Process.StartInfo.Arguments = $"-i \"{filePath}\" -progress pipe:1 {args} \"{Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filePath) + ext)}\"";
                _Process.Start();
                Trace.TraceInformation("Process ID: " + _Process.Id);
                _Process.BeginOutputReadLine();
                await ReadStdErr();
                await _Process.WaitForExitAsync();
                Trace.TraceInformation("Process Exit Code: " + _Process.ExitCode);
                lock (_CancelQLock)
                {
                    if (_CancelQ)
                    {
                        _CancelQ = false;
                        _Process.CleanStop(this);
                        return filePath;                 //returns file name if canceled -- implememnt in ui
                    }
                }
                _TotalPrevFrameProgress += _FilesDict[filePath];
            }
            _Process.CleanStop(this);
            return "0";
        }
        public void StopProfile()
        {
            lock (_CancelQLock)
            {
                _CancelQ = true;
                _Process.Refresh();
                lock (_IsDisposedLock)
                {
                    if (!_Process.HasExited)
                    {
                        _Process.StandardInput.WriteLine("q");
                        _Process.WaitForExit();
                        _Process.Dispose();
                    }
                    else if (!_IsDisposed)
                    {
                        _Process.Dispose();
                        _IsDisposed = true;
                    }
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
                if (String.IsNullOrEmpty(stdout) && String.IsNullOrEmpty(stderr))
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
            var sr = _Process.StandardError;
            var sb = new StringBuilder();
            while (!sr.EndOfStream)
            {
                var inputChar = (char)sr.Read();
                sb.Append(inputChar);
                if (sb.EndsWith(Environment.NewLine))
                {
                    var line = sb.ToString();
                    sb = new StringBuilder();
                    Trace.TraceInformation(line);
                    /*if (line.Contains("frame="))
                    {
                        _lastFrame = Int32.Parse(Regex.Match(line, "(?<=frame=\\s+)\\d+").Value);
                        //Trace.TraceInformation(_lastFrame);
                        UIProgress.Report((_lastFrame + _totalPrevFrameProgress) / _totalDirFrames);
                    }*/
                }
                else if (sb.EndsWith("already exists. Overwrite? [y/N] "))
                {
                    Trace.TraceInformation("Overwrite prompt found");
                    var line = sb.ToString();
                    if (_ViewModel!.AutoOverwriteCheck)
                    {
                        await _Process.StandardInput.WriteLineAsync("y");
                    }
                    else
                    {
                        await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            var msgBox = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(new MessageBox.Avalonia.DTO.MessageBoxStandardParams
                            {
                                ButtonDefinitions = ButtonEnum.YesNo,
                                ContentTitle = "Overwrite",
                                ContentHeader = "Overwrite?",
                                ContentMessage = $"The file \"{line.Split(@"'")[1].Split(@"'")[0]}\" already exists, would you like to overwrite it?"
                            });
                            var app = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
                            var result = await msgBox.ShowDialog(app.MainWindow);
                            if (result == ButtonResult.Yes)
                            {
                                await _Process.StandardInput.WriteLineAsync("y");
                            }
                            else
                            {
                                await _Process.StandardInput.WriteLineAsync("n");
                            }
                        }, DispatcherPriority.MaxValue);
                        sb = new StringBuilder();
                    }
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
                    _LastFrame = Int32.Parse(e.Data.Split("=")[1].Trim()); //trim
                    Trace.TraceInformation(_LastFrame.ToString());
                    var progress = ((double)_LastFrame + _TotalPrevFrameProgress) / _TotalDirFrames;
                    Trace.TraceInformation(progress.ToString());
                    _UIProgress!.Report(((double)_LastFrame + _TotalPrevFrameProgress) / _TotalDirFrames);
                }
            }
        }
    }
}