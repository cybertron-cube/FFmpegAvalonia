using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FFmpegAvalonia.AppSettingsX;

namespace FFmpegAvalonia.ViewModels
{
    internal class DescriptionData
    {
        public string SourceDir { get; set; } = "";
        public string OutputDir { get; set; } = "";
        public string FileExt { get; set; } = "";
        private readonly object fieldLock = new();
        private string _CurrentFileName = "";
        public string CurrentFileName
        {
            get
            {
                lock (fieldLock)
                {
                    return _CurrentFileName;
                }
            }
            set
            {
                lock (fieldLock)
                {
                    _CurrentFileName = value;
                }
            }
        }
        private long _CurrentFileNumber = 0;
        public long CurrentFileNumber
        {
            get
            {
                return Interlocked.Read(ref _CurrentFileNumber);
            }
            set
            {
                Interlocked.Exchange(ref _CurrentFileNumber, value);
            }
        }
        public int FileCount { get; set; }
        public Profile Profile { get; set; } = new();
        public ItemState State { get; set; } = ItemState.Awaiting;
        public ItemTask Type { get; set; }
        public ItemLabelProgressType LabelProgressType { get; set; }
        public ItemProgressBarType ProgressBarType { get; set; }
    }
}
