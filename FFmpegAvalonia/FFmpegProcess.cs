using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace FFmpegAvalonia
{
    public class FFmpegProcess : Process
    {
        private readonly string _FFmpegPath;
        private readonly string FFmpeg;
        private readonly string FFprobe;
        public FFmpegProcess(string ffMpegDir)
        {
            _FFmpegPath = ffMpegDir;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                FFmpeg = "ffmpeg";
                FFprobe = "ffprobe";
            }
            else
            {
                FFmpeg = "ffmpeg.exe";
                FFprobe = "ffprobe.exe";
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
            this.StartInfo.FileName = Path.Combine(_FFmpegPath, ffProc);
            this.StartInfo.Arguments = args;
            this.Start();
            Trace.TraceInformation(ffProc + " " + args);
            Trace.TraceInformation("Process ID: " + this.Id);
        }
    }
}
