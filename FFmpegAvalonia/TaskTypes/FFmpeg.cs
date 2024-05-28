using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ExtensionMethods;
using System.Threading;
using System.Linq;
using FFmpegAvalonia.Models;
using Serilog;
using Serilog.Context;

namespace FFmpegAvalonia.TaskTypes
{
    public class FFmpeg
    {
        private FFmpegProcess _ffProcess;
        private readonly string _ffMpegPath;
        private readonly Dictionary<string, double> _filesDict = new();
        private double _endTime;
        private double _totalDirTimeSeconds;
        private double _currentDirTimeSeconds;
        private IProgress<double>? _uiProgress;
        private string _lastStdErrLine = string.Empty;
        private readonly ILogger _log = Log.ForContext<FFmpeg>();
        
        public FFmpeg(string ffmpegDir)
        {
            _ffMpegPath = ffmpegDir;
            _ffProcess = new FFmpegProcess(_ffMpegPath)
            {
                StartInfo = DefaultStartInfo(),
                EnableRaisingEvents = true,
            };
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
        
        public string SetProgression(string dir, string ext) => GetDuration(dir, '*' + ext);
        
        public async Task<(int, string)> RunProfile(string name, string args, string outputDir, string ext, IProgress<double> progress, CancellationToken ct, bool detachProcess = false)
        {
            //Start out having progress bar show prog of entire dir
            //Progress would be current progress plus the sum of the files already done
            _uiProgress = progress;
            _log.Information("Starting transcode to {OutputDir}", outputDir);
            foreach (var filePath in _filesDict.Keys)
            {
                _log.Information(detachProcess
                    ? "Creating detached ffmpeg process"
                    : "Creating attached ffmpeg process");
                NewFFProcess(detachProcess);
                
                if (ct.IsCancellationRequested)
                {
                    if (detachProcess)
                    {
                        _ffProcess.Kill();
                        await _ffProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    DisposeFFProcess();
                    _log.Information("Canceled on {FilePath}", filePath);
                    return (-1, filePath);
                }
                
                var ffArgs = args.Contains(" -y ")
                    ? $"-progress pipe:1 -i \"{filePath}\" {args} \"{Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filePath) + ext)}\""
                    : $"-progress pipe:1 -y -i \"{filePath}\" {args} \"{Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filePath) + ext)}\"";
                _ffProcess.StartMpeg(ffArgs);
                
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
                    try
                    {
                        var outputTask = ReadStreamAsync(_ffProcess.StandardOutput, "stdout", OnNewLineStandardOutputProfile);
                        var errorTask = ReadStreamAsync(_ffProcess.StandardError, "stderr", OnNewLineStandardError);
                        var exitTask = WaitFFmpegAsync(name, ct);

                        await Task.WhenAll(outputTask, errorTask, exitTask);
                    }
                    catch (Exception exc)
                    {
                        _ffProcess.Kill();
                        _ffProcess.Dispose();
                        _log.Error(exc, "");
                        return (-31, exc.ToString());
                    }
                }
                
                _log.Information("Process Exit Code: {ExitCode}",  _ffProcess.ExitCode);
                
                if (_ffProcess.ExitCode != 0)
                {
                    var exitCode = _ffProcess.ExitCode;
                    DisposeFFProcess();
                    _log.Information("Exited with code {ExitCode} on {FilePath}", exitCode, filePath);
                    return (exitCode, _lastStdErrLine);
                }

                DisposeFFProcess();
                _log.Information("File transcode, \"{FilePath}\", complete", filePath);
            }
            
            if (detachProcess)
            {
                _uiProgress.Report(1);
            }
            
            return (0, string.Empty);
        }

        public async Task<(int, string)> TrimDir(IEnumerable<TrimData> trimData, string sourceDir, string outputDir, IProgress<double> progress, ListViewData item, CancellationToken ct, bool detachProcess = false)
        {
            _uiProgress = progress;
            var overwrite = outputDir == string.Empty || sourceDir == outputDir;
            
            var trimDataValidTimeCodes = trimData.Where(x => x.StartTime is not null && x.EndTime is not null);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                item.Description.FileCount = trimDataValidTimeCodes.Count();
                item.Label = $"{item.Name} ({item.Description.CurrentFileNumber}/{item.Description.FileCount})";
            });
            
            _log.Information("Starting trim to {OutputDir} from {SourceDir}", outputDir, sourceDir);
            foreach (var data in trimDataValidTimeCodes)
            {
                _endTime = data.EndTime!.GetTotalSeconds();
                
                _log.Information(detachProcess
                    ? "Creating detached ffmpeg process"
                    : "Creating attached ffmpeg process");
                
                NewFFProcess(detachProcess);
                
                if (ct.IsCancellationRequested)
                {
                    if (detachProcess)
                    {
                        _ffProcess.Kill();
                        await _ffProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    DisposeFFProcess();
                    _log.Information("Canceled on {FilePath}", data.FileInfo.FullName);
                    return (-1, data.FileInfo.FullName);
                }

                var newFile = overwrite ?
                    Path.Combine(data.FileInfo.Directory.FullName, $"_{data.FileInfo.Name}")
                    : Path.Combine(outputDir, data.FileInfo.Name);
                
                _ffProcess.StartMpeg($"-progress pipe:1 -y -ss {data.StartTime!.FormattedString} -to {data.EndTime.FormattedString} -i \"{data.FileInfo.FullName}\" -map 0 -codec copy \"{newFile}\"");
                
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
                    try
                    {
                        var outputTask = ReadStreamAsync(_ffProcess.StandardOutput, "stdout", OnNewLineStandardOutputTrim);
                        var errorTask = ReadStreamAsync(_ffProcess.StandardError, "stderr", OnNewLineStandardError);
                        var exitTask = WaitFFmpegAsync("trim", ct);
                        
                        await Task.WhenAll(outputTask, errorTask, exitTask);
                    }
                    catch (Exception exc)
                    {
                        _ffProcess.Kill();
                        _ffProcess.Dispose();
                        _log.Error(exc, "");
                        return (-31, exc.ToString());
                    }
                }
                
                _log.Information("Process Exit Code: {ExitCode}", _ffProcess.ExitCode);
                if (ct.IsCancellationRequested)
                {
                    if (detachProcess)
                    {
                        _ffProcess.Kill();
                        await _ffProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    DisposeFFProcess();
                    _log.Information("Canceled on {FilePath}", data.FileInfo.FullName);
                    return (-1, data.FileInfo.FullName);
                }

                if (_ffProcess.ExitCode == 0 && overwrite)
                {
                    var rename = data.FileInfo.FullName;
                    data.FileInfo.Delete();
                    File.Move(newFile, rename);
                }
                else if (_ffProcess.ExitCode != 0)
                {
                    var exitCode = _ffProcess.ExitCode;
                    DisposeFFProcess();
                    _log.Information("Exited with code {ExitCode} on {FilePath}", exitCode, data.FileInfo.FullName);
                    return (exitCode, _lastStdErrLine);
                }

                DisposeFFProcess();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    item.Label = $"{item.Name} ({++item.Description.CurrentFileNumber}/{item.Description.FileCount})";
                });
                _log.Information("File trim, \"{FilePath}\", complete", data.FileInfo.FullName);
            }
            return (0, string.Empty);
        }
        
        private void NewFFProcess(bool detachProcess = false)
        {
            if (detachProcess)
            {
                _ffProcess = new FFmpegProcess(_ffMpegPath)
                {
                    StartInfo = new ProcessStartInfo(),
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

        private string GetDuration(string dir, string searchPattern)
        {
            NewFFProcess();
            var dirInfo = new DirectoryInfo(dir);
            var files = dirInfo.EnumerateFiles(searchPattern);
            var sb = new StringBuilder();
            foreach (var file in files)
            {
                sb.Append(file.FullName);
                    
                _ffProcess.StartProbe($"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{file.FullName}\"");
                var totalSeconds = Convert.ToDouble(_ffProcess.StandardOutput.ReadToEnd().Trim()[..^3]);
                
                _ffProcess.WaitForExit();
                
                _log.Information("Process Exit Code: {ExitCode}", _ffProcess.ExitCode);
                
                _filesDict.Add(file.FullName, totalSeconds);
                _totalDirTimeSeconds += totalSeconds;
                sb.Append(" -- " + totalSeconds + Environment.NewLine);
            }
            _ffProcess.Dispose();
            return sb.ToString();
        }
        
        private async Task ReadStreamAsync(StreamReader stream, string name, Action<string>? onNewLine = null)
        {
            using var logContext = LogContext.PushProperty("StreamName", name);
            
            var buffer = new char[4096];
            var sb = new StringBuilder();
            int charRead;
            while ((charRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                if (charRead >= buffer.Length * 0.8)
                {
                    _log.Warning("Buffer is approaching overflow -> Chars Read: {Chars} | Buffer Length: {Length}", charRead, buffer.Length);
                }
                else
                {
                    _log.Information("Chars Read: {Chars} | Buffer Length: {Length}", charRead, buffer.Length);
                }
            
                for (int i = 0; i < charRead; i++)
                {
                    if (buffer[i] == '\n')
                    {
                        var line = buffer[i - 1] == '\r' ? sb.ToStringTrimEnd("\r") : sb.ToString();
                        sb.Clear();
                        _log.Information("{Data}", line);
                        onNewLine?.Invoke(line);
                    }
                    else
                    {
                        sb.Append(buffer[i]);
                    }
                }
            }
        }

        private void OnNewLineStandardOutputProfile(string line)
        {
            // EX: out_time_ms=659434000
            if (line.Contains("out_time_us"))
            {
                // Ignore the last 3 characters since for some reason ffmpeg outputs microseconds instead of milliseconds
                var currentTimeMs = Convert.ToDouble(line.Split('=')[1][..^3]);
                if (currentTimeMs < 0) return;
                var currentTimeSeconds = currentTimeMs / 1000;
                _currentDirTimeSeconds += currentTimeSeconds;
                _uiProgress?.Report(_currentDirTimeSeconds / _totalDirTimeSeconds);
            }
            else if (line.Contains("progress=end"))
            {
                _uiProgress?.Report(1);
            }
        }
        
        private void OnNewLineStandardOutputTrim(string line)
        {
            // EX: out_time_ms=659434000
            if (line.Contains("out_time_us"))
            {
                // Ignore the last 3 characters since for some reason ffmpeg outputs microseconds instead of milliseconds
                var currentTimeMs = Convert.ToDouble(line.Split('=')[1][..^3]);
                if (currentTimeMs < 0) return;
                var progress = currentTimeMs / _endTime;
                _uiProgress?.Report(progress);
            }
            else if (line.Contains("progress=end"))
            {
                _uiProgress?.Report(1);
            }
        }
        
        private void OnNewLineStandardError(string line)
        {
            _lastStdErrLine = line;
        }
        
        public void DisposeFFProcess()
        {
            if (_ffProcess is { HasStarted: true, HasExited: false })
                QuitFFmpegProcess();
            _ffProcess.Dispose();
        }

        private async Task WaitFFmpegAsync(string commandName, CancellationToken ct)
        {
            try
            {
                await _ffProcess.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                _log.Information("{Command} canceled", commandName);
                await QuitFFmpegProcessAsync();
            }
        }

        private async Task QuitFFmpegProcessAsync()
        {
            await _ffProcess.StandardInput.WriteAsync('q');
            await _ffProcess.StandardInput.FlushAsync();
            try
            {
                await _ffProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException ex)
            {
                _log.Error(ex, "FFmpeg would not gracefully exit, the process will now be killed");
                _ffProcess.Kill();
            }
        }
    
        private void QuitFFmpegProcess()
        {
            _ffProcess.StandardInput.Write('q');
            _ffProcess.StandardInput.Flush();
            try
            {
                _ffProcess.WaitForExit(5000);
            }
            catch (TimeoutException ex)
            {
                _log.Error(ex, "FFmpeg would not gracefully exit, the process will now be killed");
                _ffProcess.Kill();
            }
        }
    }
}