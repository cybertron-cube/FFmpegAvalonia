using ReactiveUI;
using ReactiveUI.Validation.Abstractions;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Contexts;
using ReactiveUI.Validation.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using ReactiveUI.Validation.States;
using System.Xml.Linq;

namespace FFmpegAvalonia.ViewModels
{
    public class MainWindowViewModel : ReactiveValidationObject
    {
        public MainWindowViewModel()
        {
            IObservable<bool> textBoxObserv =
                this.WhenAnyValue(
                    x => x.SourceDirText,
                    x => x.OutputDirText,
                    (sourceDir, outputDir) => sourceDir != outputDir);
            IObservable<bool> outputBoxObserv =
                this.WhenAnyValue(
                    x => x.TrimCheck,
                    x => x.OutputDirText,
                    (trimCheck, outputDirText) => trimCheck || Directory.Exists(outputDirText));
            IObservable<bool> extValidObservNoFiles =
                this.WhenAnyValue(
                    x => x.SourceDirText,
                    x => x.ExtBoxIsEnabled,
                    x => x.ExtText,
                    (sourceDirText, extBoxIsEnabled, extText) => !extBoxIsEnabled ||
                                                                (!String.IsNullOrWhiteSpace(sourceDirText)
                                                                && Directory.Exists(sourceDirText)
                                                                && Directory.EnumerateFiles(sourceDirText, $"*{extText}").Any()));
            IObservable<bool> extValidObserv =
                this.WhenAnyValue(
                    x => x.ExtBoxIsEnabled,
                    x => x.ExtText,
                    (extBoxIsEnabled, extText) => !extBoxIsEnabled || !String.IsNullOrWhiteSpace(extText));
            this.ValidationRule(
                vm => vm.SourceDirText,
                sourceDirText => Directory.Exists(sourceDirText),
                "You must specify a valid source directory");
            this.ValidationRule(
                vm => vm.SourceDirText,
                textBoxObserv,
                "Directories cannot be the same");
            this.ValidationRule(
                vm => vm.OutputDirText,
                outputBoxObserv,
                "You must specify a valid output directory");
            this.ValidationRule(
                vm => vm.OutputDirText,
                textBoxObserv,
                "Directories cannot be the same");
            this.ValidationRule(
                vm => vm.ExtText,
                extValidObserv,
                "You must specify a valid extension");
            this.ValidationRule(
                vm => vm.ExtText,
                extValidObservNoFiles,
                "Source doesn't contain files with this extension");
        }
        private string _sourceDirText = String.Empty;
        public string SourceDirText
        {
            get => _sourceDirText;
            set => this.RaiseAndSetIfChanged(ref _sourceDirText, value);
        }
        private string _outputDirText = String.Empty;
        public string OutputDirText
        {
            get => _outputDirText;
            set => this.RaiseAndSetIfChanged(ref _outputDirText, value);
        }
        private bool _extBoxIsEnabled = true;
        public bool ExtBoxIsEnabled
        {
            get => _extBoxIsEnabled;
            set => this.RaiseAndSetIfChanged(ref _extBoxIsEnabled, value);
        }
        private string _extText = String.Empty;
        public string ExtText
        {
            get => _extText;
            set => this.RaiseAndSetIfChanged(ref _extText, value);
        }
        private bool _profileBoxIsEnabled = true;
        public bool ProfileBoxIsEnabled
        {
            get => _profileBoxIsEnabled;
            set => this.RaiseAndSetIfChanged(ref _profileBoxIsEnabled, value);
        }
        private bool _copyCheckIsEnabled = true;
        public bool CopyCheckIsEnabled
        {
            get => _copyCheckIsEnabled;
            set => this.RaiseAndSetIfChanged(ref _copyCheckIsEnabled, value);
        }
        private long _copySourceCheck = 0;
        public bool CopySourceCheck
        {
            get => Interlocked.Read(ref _copySourceCheck) == 1;
            set
            {
                Interlocked.Exchange(ref _copySourceCheck, Convert.ToInt64(value));
                TrimCheckIsEnabled = !value;
                ProfileBoxIsEnabled = !value;
                this.RaisePropertyChanged(nameof(CopySourceCheck));
            }
        }
        private bool _trimCheckIsEnabled = true;
        public bool TrimCheckIsEnabled
        {
            get => _trimCheckIsEnabled;
            set => this.RaiseAndSetIfChanged(ref _trimCheckIsEnabled, value);
        }
        private long _trimCheck = 0;
        public bool TrimCheck
        {
            get => Interlocked.Read(ref _trimCheck) == 1;
            set
            {
                Interlocked.Exchange(ref _trimCheck, Convert.ToInt64(value));
                CopyCheckIsEnabled = !value;
                ProfileBoxIsEnabled = !value;
                this.RaisePropertyChanged(nameof(TrimCheck));
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
    }
}
