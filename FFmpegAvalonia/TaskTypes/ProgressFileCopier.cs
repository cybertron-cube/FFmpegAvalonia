using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using FFmpegAvalonia.ViewModels;
using AvaloniaMessageBox;
using FFmpegAvalonia.Models;

namespace FFmpegAvalonia.TaskTypes
{
    public delegate void ProgressChangeDelegate(double Percentage);
    public delegate void Completedelegate();

    public class ProgressFileCopier
    {
        private string _sourceFilePath = String.Empty;
        private string _outputFilePath = String.Empty;
        private readonly IProgress<double> _uIProgress;
        private readonly ListViewData _item;
        private readonly MainWindowViewModel _viewModel;
        public event ProgressChangeDelegate OnProgressChanged;
        public event Completedelegate OnComplete;

        public ProgressFileCopier(IProgress<double> progress, ListViewData item, MainWindowViewModel viewModel)
        {
            _uIProgress = progress;
            _item = item;
            _viewModel = viewModel;

            OnProgressChanged += delegate { };
            OnComplete += delegate { };
        }
        public void CopyFile(string sourceFilePath, string outputFilePath) //change this to match the override below
        {
            _sourceFilePath = sourceFilePath;
            _outputFilePath = outputFilePath;

            byte[] buffer = new byte[1024 * 1024]; // 1MB buffer
            bool cancelFlag = false;

            using (FileStream source = new FileStream(_sourceFilePath, FileMode.Open, FileAccess.Read))
            {
                long fileLength = source.Length;
                using (FileStream dest = new FileStream(_outputFilePath, FileMode.CreateNew, FileAccess.Write))
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
        private void CopyFile(CancellationToken ct)
        {
            byte[] buffer = new byte[1024 * 1024]; // 1MB buffer

            using (FileStream source = new(_sourceFilePath, FileMode.Open, FileAccess.Read))
            {
                long fileLength = source.Length;
                using (FileStream dest = new(_outputFilePath, FileMode.CreateNew, FileAccess.Write))
                {
                    long totalBytes = 0;
                    int currentBlockSize = 0;

                    while ((currentBlockSize = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        totalBytes += currentBlockSize;
                        double percentage = (double)totalBytes / fileLength;
                        dest.Write(buffer, 0, currentBlockSize);
                        OnProgressChanged(percentage);
                        if (ct.IsCancellationRequested)
                        {
                            return;
                        }
                    }
                }
            }
            OnComplete();
        }
        public async Task<(int, string)> CopyDirectory(string sourceDir, string outputDir, string ext, CancellationToken ct)
        {
            DirectoryInfo dirInfo = new(sourceDir);
            var files = dirInfo.EnumerateFiles(ext);
            //Total = files.Count();
            OnProgressChanged += ProgressFileCopier_OnProgressChanged;
            OnComplete += ProgressFileCopier_OnComplete;
            foreach (FileInfo file in files)
            {
                _item.Description.CurrentFileNumber += 1;
                _item.Label = $"{_item.Name} ({_item.Description.CurrentFileNumber}/{_item.Description.FileCount})";
                _sourceFilePath = file.FullName;
                _outputFilePath = Path.Combine(outputDir, file.Name);
                _item.Description.CurrentFileName = file.Name;
                if (File.Exists(_outputFilePath))
                {
                    if (!_viewModel.AutoOverwriteCheck)
                    {
                        bool overwrite = false;
                        await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            var msgBox = MessageBox.GetMessageBox(new MessageBoxParams
                            {
                                Buttons = MessageBoxButtons.YesNo,
                                Title = "Overwrite?",
                                Message = $"The file \"{_outputFilePath}\" already exists, would you like to overwrite it?"
                            });
                            var app = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
                            var result = await msgBox.ShowDialog(app.MainWindow);
                            if (result == MessageBoxResult.Yes)
                            {
                                overwrite = true;
                                File.Delete(_outputFilePath);
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
                        File.Delete(_outputFilePath);
                    }
                }
                CopyFile(ct);
                if (ct.IsCancellationRequested)
                {
                    File.Delete(_outputFilePath);
                    return (-1, file.FullName);
                }
            }
            return (0, String.Empty);
        }
        private void ProgressFileCopier_OnComplete()
        {
            _item.Label = $"{_item.Name} ({_item.Description.CurrentFileNumber}/{_item.Description.FileCount})";
        }
        private void ProgressFileCopier_OnProgressChanged(double Percentage)
        {
            _uIProgress.Report(Percentage);
        }
    }
}
