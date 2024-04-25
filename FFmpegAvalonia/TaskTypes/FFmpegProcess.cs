using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;

namespace FFmpegAvalonia.TaskTypes
{
    public class FFmpegProcess : Process
    {
        private readonly string _FFmpegPath;
        private readonly string FFmpeg;
        private readonly string FFprobe;
        public FFmpegProcess(string ffMpegDir)
        {
            _FFmpegPath = ffMpegDir;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                FFmpeg = "ffmpeg.exe";
                FFprobe = "ffprobe.exe";
            }
            else
            {
                FFmpeg = "ffmpeg";
                FFprobe = "ffprobe";
            }
        }
        public void StartMpeg(string args)
        {
            StartFF(args, FFmpeg);
        }
        public void StartProbe(string args)
        {
            StartFF(args, FFprobe);
        }
        private void StartFF(string args, string ffProc)
        {
            StartInfo.FileName = Path.Combine(_FFmpegPath, ffProc);
            StartInfo.Arguments = args;
            Start();
            Trace.TraceInformation(ffProc + " " + args);
            Trace.TraceInformation("Process ID: " + Id);
        }
    }
}
