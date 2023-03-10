using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FFmpegAvalonia.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        private long _copySourceCheck = 0;
        public bool CopySourceCheck
        {
            get => Interlocked.Read(ref _copySourceCheck) == 1;
            set
            {
                Interlocked.Exchange(ref _copySourceCheck, Convert.ToInt64(value));
                this.RaisePropertyChanged(nameof(CopySourceCheck));
            }
        }
        private long _autoOverwriteCheck = 0;
        public bool AutoOverwriteCheck
        {
            get => Interlocked.Read(ref _autoOverwriteCheck) == 1;
            set
            {
                Interlocked.Exchange(ref _autoOverwriteCheck, Convert.ToInt64(value));
                this.RaisePropertyChanged(nameof(AutoOverwriteCheck));
            }
        }
        /*private bool _autoOverwriteCheck;
        public bool AutoOverwriteCheck
        {
            get => _autoOverwriteCheck;
            set => this.RaiseAndSetIfChanged(ref _autoOverwriteCheck, value);
        }*/
    }
}
