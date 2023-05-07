using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using FFmpegAvalonia.ViewModels;
using AvaloniaMessageBox;

namespace FFmpegAvalonia.TaskTypes
{
    public delegate void ProgressChangeDelegate(double Percentage);
    public delegate void Completedelegate();

    public class ProgressFileCopier
    {
        private string SourceFilePath = string.Empty;
        private string OutputFilePath = string.Empty;
        //private int Total;
        private readonly IProgress<double> _UIProgress;
        private readonly ListViewData _Item;
        private readonly MainWindowViewModel _ViewModel;
        public event ProgressChangeDelegate OnProgressChanged;
        public event Completedelegate OnComplete;
        private long _CancelFlag;
        public bool CancelFlag
        {
            get => Interlocked.Read(ref _CancelFlag) == 1;
            set => Interlocked.Exchange(ref _CancelFlag, Convert.ToInt64(value));
        }

        public ProgressFileCopier(IProgress<double> progress, ListViewData item, MainWindowViewModel viewModel)
        {
            _UIProgress = progress;
            _Item = item;
            _ViewModel = viewModel;

            OnProgressChanged += delegate { };
            OnComplete += delegate { };
        }
        public void CopyFile(string sourceFilePath, string outputFilePath) //change this to match the override below
        {
            SourceFilePath = sourceFilePath;
            OutputFilePath = outputFilePath;

            byte[] buffer = new byte[1024 * 1024]; // 1MB buffer
            bool cancelFlag = false;

            using (FileStream source = new FileStream(SourceFilePath, FileMode.Open, FileAccess.Read))
            {
                long fileLength = source.Length;
                using (FileStream dest = new FileStream(OutputFilePath, FileMode.CreateNew, FileAccess.Write))
                {
                    long totalBytes = 0;
                    int currentBlockSize = 0;

                    while ((currentBlockSize = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        totalBytes += currentBlockSize;
                        double percentage = totalBytes * 100.0 / fileLength;

                        dest.Write(buffer, 0, currentBlockSize);

                        cancelFlag = false;
                        OnProgressChanged(percentage);

                        if (cancelFlag == true)
                        {
                            // Delete dest file here
                            break;
                        }
                    }
                }
            }

            OnComplete();
        }
        private void CopyFile()
        {
            byte[] buffer = new byte[1024 * 1024]; // 1MB buffer

            using (FileStream source = new(SourceFilePath, FileMode.Open, FileAccess.Read))
            {
                long fileLength = source.Length;
                using (FileStream dest = new(OutputFilePath, FileMode.CreateNew, FileAccess.Write))
                {
                    long totalBytes = 0;
                    int currentBlockSize = 0;

                    while ((currentBlockSize = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        totalBytes += currentBlockSize;
                        double percentage = (double)totalBytes / fileLength;
                        dest.Write(buffer, 0, currentBlockSize);
                        OnProgressChanged(percentage);
                        if (CancelFlag)
                        {
                            return;
                        }
                    }
                }
            }
            OnComplete();
        }
        public async Task<string> CopyDirectory(string sourceDir, string outputDir, string ext)
        {
            DirectoryInfo dirInfo = new(sourceDir);
            var files = dirInfo.EnumerateFiles(ext);
            //Total = files.Count();
            OnProgressChanged += ProgressFileCopier_OnProgressChanged;
            OnComplete += ProgressFileCopier_OnComplete;
            foreach (FileInfo file in files)
            {
                _Item.Description.CurrentFileNumber += 1;
                _Item.Label = $"{_Item.Name} ({_Item.Description.CurrentFileNumber}/{_Item.Description.FileCount})";
                SourceFilePath = file.FullName;
                OutputFilePath = Path.Combine(outputDir, file.Name);
                _Item.Description.CurrentFileName = file.Name;
                if (File.Exists(OutputFilePath))
                {
                    if (!_ViewModel.AutoOverwriteCheck)
                    {
                        bool overwrite = false;
                        await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            var msgBox = MessageBox.GetMessageBox(new MessageBoxParams
                            {
                                Buttons = MessageBoxButtons.YesNo,
                                Title = "Overwrite?",
                                Message = $"The file \"{OutputFilePath}\" already exists, would you like to overwrite it?"
                            });
                            var app = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
                            var result = await msgBox.ShowDialog(app.MainWindow);
                            if (result == MessageBoxResult.Yes)
                            {
                                overwrite = true;
                                File.Delete(OutputFilePath);
                            }
                            else
                            {
                                overwrite = false;
                            }
                        }, DispatcherPriority.MaxValue);
                        if (!overwrite)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        File.Delete(OutputFilePath);
                    }
                }
                CopyFile();
                if (CancelFlag)
                {
                    File.Delete(OutputFilePath);
                    return file.FullName;
                }
            }
            return "0";
        }
        public void Stop()
        {
            CancelFlag = true;
        }
        private void ProgressFileCopier_OnComplete()
        {
            _Item.Label = $"{_Item.Name} ({_Item.Description.CurrentFileNumber}/{_Item.Description.FileCount})";
        }
        private void ProgressFileCopier_OnProgressChanged(double Percentage)
        {
            _UIProgress.Report(Percentage);
        }
    }
}
