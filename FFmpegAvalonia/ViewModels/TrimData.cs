using ExtensionMethods;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFmpegAvalonia.ViewModels
{
    public class TrimData : ReactiveObject
    {
        public TrimData(FileInfo fileInfo)
        {
            _fileInfo = fileInfo;
        }
        private FileInfo _fileInfo;
        public FileInfo FileInfo
        {
            get => _fileInfo;
            set => this.RaiseAndSetIfChanged(ref _fileInfo, value);
        }
        public string Name
        {
            get => _fileInfo.NameWithoutExtension();
        }
        private TimeCode? _startTime;
        public TimeCode? StartTime
        {
            get => _startTime;
            set => this.RaiseAndSetIfChanged(ref _startTime, value);
        }
        private TimeCode? _endTime;
        public TimeCode? EndTime
        {
            get => _endTime;
            set => this.RaiseAndSetIfChanged(ref _endTime, value);
        }
    }
}
