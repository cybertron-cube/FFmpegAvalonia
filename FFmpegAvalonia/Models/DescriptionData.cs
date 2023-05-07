using System;
using System.Collections.ObjectModel;
using System.Threading;
using FFmpegAvalonia.AppSettingsX;
using FFmpegAvalonia.TaskTypes;
using ReactiveUI;

namespace FFmpegAvalonia.Models
{
    public class DescriptionData : ReactiveObject
    {
        private string _sourceDir = String.Empty;
        public string SourceDir
        {
            get => _sourceDir;
            set => this.RaiseAndSetIfChanged(ref _sourceDir, value);
        }
        private string _outputDir = String.Empty;
        public string OutputDir
        {
            get => _outputDir;
            set => this.RaiseAndSetIfChanged(ref _outputDir, value);
        }
        private string _fileExt = String.Empty;
        public string FileExt
        {
            get => _fileExt;
            set => this.RaiseAndSetIfChanged(ref _fileExt, value);
        }
        public ObservableCollection<TrimData>? TrimData;
        public AWSTask? AWS;
        private readonly object fieldLock = new();
        private string _currentFileName = String.Empty;
        public string CurrentFileName
        {
            get
            {
                lock (fieldLock)
                {
                    return _currentFileName;
                }
            }
            set
            {
                lock (fieldLock)
                {
                    this.RaiseAndSetIfChanged(ref _currentFileName, value);
                }
            }
        }
        private long _currentFileNumber = 0;
        public long CurrentFileNumber
        {
            get
            {
                return Interlocked.Read(ref _currentFileNumber);
            }
            set
            {
                Interlocked.Exchange(ref _currentFileNumber, value);
                this.RaisePropertyChanged(nameof(CurrentFileNumber));
            }
        }
        private int _fileCount = 0;
        public int FileCount
        {
            get => _fileCount;
            set => this.RaiseAndSetIfChanged(ref _fileCount, value);
        }
        private Profile _profile = new();
        public Profile Profile
        {
            get => _profile;
            set => this.RaiseAndSetIfChanged(ref _profile, value);
        }
        private ItemState _state = ItemState.Awaiting;
        public ItemState State
        {
            get => _state;
            set => this.RaiseAndSetIfChanged(ref _state, value);
        }
        private ItemTask _task;
        public ItemTask Task
        {
            get => _task;
            set => this.RaiseAndSetIfChanged(ref _task, value);
        }
        private ItemLabelProgressType _labelProgressType;
        public ItemLabelProgressType LabelProgressType
        {
            get => _labelProgressType;
            set => this.RaiseAndSetIfChanged(ref _labelProgressType, value);
        }
        private ItemProgressBarType _progressBarType;
        public ItemProgressBarType ProgressBarType
        {
            get => _progressBarType;
            set => this.RaiseAndSetIfChanged(ref _progressBarType, value);
        }
    }
}
