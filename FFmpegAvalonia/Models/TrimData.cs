using ExtensionMethods;
using ReactiveUI;
using System.IO;

namespace FFmpegAvalonia.Models
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
            get => _fileInfo.GetNameWithoutExtension();
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
