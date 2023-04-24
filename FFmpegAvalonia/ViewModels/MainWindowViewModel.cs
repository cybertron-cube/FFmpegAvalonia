using ReactiveUI;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Collections.ObjectModel;
using System.Reactive;
using FFmpegAvalonia.AppSettingsX;
using System.Diagnostics;
using Avalonia.Media;
using System.Reactive.Linq;
using CyberFileUtils;
using Avalonia.Threading;
using Avalonia.Controls;
using AvaloniaMessageBox;
using Cybertron.CUpdater;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Net.Http;

namespace FFmpegAvalonia.ViewModels
{
    public class MainWindowViewModel : ReactiveValidationObject
    {
        public MainWindowViewModel(AppSettings appSettings)
        {
            #region Field/Property Initializers
            _taskTypeItems = Enum.GetValues(typeof(ItemTask)).Cast<ItemTask>().ToList();
            AppSettings = appSettings;
            _profileItems = new ObservableCollection<string>(appSettings.Profiles.Keys);
            #endregion
            #region Observables
            IObservable<bool> textBoxEqualObserv =
                this.WhenAnyValue(
                    x => x.SourceDirText,
                    x => x.OutputDirText,
                    (sourceDir, outputDir) => sourceDir != outputDir);
            IObservable<bool> outputBoxObserv =
                this.WhenAnyValue(
                    x => x.SelectedTaskType,
                    x => x.OutputDirText,
                    (trimCheck, outputDirText) => trimCheck == ItemTask.Trim || Directory.Exists(outputDirText));
            IObservable<bool> extValidObservNoFiles =
                this.WhenAnyValue(
                    x => x.SourceDirText,
                    x => x.ExtText,
                    (sourceDirText, extText) => !String.IsNullOrWhiteSpace(extText)
                                                && !String.IsNullOrWhiteSpace(sourceDirText)
                                                                && Directory.Exists(sourceDirText)
                                                && Directory.EnumerateFiles(sourceDirText, $"*{extText}").Any());
            IObservable<bool> profileBoxObserv =
                this.WhenAnyValue(
                    x => x.SelectedTaskType,
                    x => x.SelectedProfile,
                    (selectedItemTask, selectedProfile) => selectedItemTask != ItemTask.Transcode
                                                           || selectedProfile != null);
            IObservable<bool> startQueueCanExec =
                TaskListItems.WhenAnyValue(
                    x => x.Count,
                    selector: (taskListCount) => taskListCount > 0);
            #endregion
            #region Validation Rules
            this.ValidationRule(
                vm => vm.SourceDirText,
                sourceDirText => Directory.Exists(sourceDirText),
                "You must specify a valid source directory");
            this.ValidationRule(
                vm => vm.SourceDirText,
                textBoxEqualObserv,
                "Directories cannot be the same");
            this.ValidationRule(
                vm => vm.OutputDirText,
                outputBoxObserv,
                "You must specify a valid output directory");
            this.ValidationRule(
                vm => vm.OutputDirText,
                textBoxEqualObserv,
                "Directories cannot be the same");
            this.ValidationRule(
                vm => vm.ExtText,
                extText => !String.IsNullOrWhiteSpace(extText),
                "You must specify a valid extension");
            this.ValidationRule(
                vm => vm.ExtText,
                extValidObservNoFiles,
                "Source doesn't contain files with this extension");
            this.ValidationRule(
                vm => vm.ProfileText,
                profileBoxObserv,
                "You must choose a profile");
            this.ValidationRule(
                vm => vm.IsQueueRunning,
                isQueueRunning => !isQueueRunning,
                " ");
            #endregion
            #region Interactions
            ShowTextEditorDialog = new Interaction<string, string?>();
            ShowTrimDialog = new Interaction<TrimWindowViewModel, bool>();
            //ShowDownloadUpdatesDialog = new Interaction<Updater.CheckUpdateResult, Unit>();
            ShowMessageBox = new Interaction<MessageBoxParams, MessageBoxResult>();
            #endregion
            #region Commands
            StartQueueCommand = ReactiveCommand.CreateFromObservable(() => Observable.StartAsync(ct => StartQueueAsync(ct)).TakeUntil(StopQueueCommand!), startQueueCanExec);
            AddToQueueCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                switch (SelectedTaskType)
                {
                    case ItemTask.Transcode:
                        AddTranscode();
                        break;
                    case ItemTask.Copy:
                        AddCopy();
                        break;
                    case ItemTask.Trim:
                        await AddTrim();
                        break;
                }
            }, this.IsValid());
            StopQueueCommand = ReactiveCommand.Create(StopQueue, StartQueueCommand.IsExecuting);
            EditorCommand = ReactiveCommand.CreateFromTask<string>(Editor);
            CheckForUpdatesCommand = ReactiveCommand.CreateFromTask(CheckForUpdates);
            OpenURLCommand = ReactiveCommand.Create<string>(OpenURL);
            #endregion
            #region Subscriptions
            StartQueueCommand.IsExecuting.Subscribe(x => IsQueueRunning = x);
            #endregion
        }
        public ReactiveCommand<Unit, Unit> AddToQueueCommand { get; }
        public ReactiveCommand<Unit, Unit> StartQueueCommand { get; }
        public ReactiveCommand<Unit, Unit> StopQueueCommand { get; }
        public ReactiveCommand<string, Unit> EditorCommand { get; }
        public ReactiveCommand<Unit, Unit> CheckForUpdatesCommand { get; }
        public ReactiveCommand<string, Unit> OpenURLCommand { get; }
        public Interaction<string, string?> ShowTextEditorDialog;
        public Interaction<TrimWindowViewModel, bool> ShowTrimDialog;
        //public Interaction<Updater.CheckUpdateResult, Unit> ShowDownloadUpdatesDialog;
        public Interaction<MessageBoxParams, MessageBoxResult> ShowMessageBox;
        private readonly AppSettings AppSettings;
        private void AddTranscode()
        {
            TaskListItems.Add(new ListViewData()
            {
                Name = Path.GetFileName(SourceDirText),
                Label = Path.GetFileName(SourceDirText),
                Description = new DescriptionData()
                {
                    SourceDir = SourceDirText,
                    OutputDir = OutputDirText,
                    FileExt = ExtText.StartsWith('.') ? ExtText : $".{ExtText}",
                    FileCount = Directory.EnumerateFiles(SourceDirText, $"*{ExtText}").Count(),//
                    Profile = SelectedProfile!,
                    Task = ItemTask.Transcode,
                    LabelProgressType = ItemLabelProgressType.None,
                    ProgressBarType = ItemProgressBarType.Directory
                },
                Background = Brushes.LightYellow
            });
        }
        private void AddCopy()
        {
            TaskListItems.Add(new ListViewData()
            {
                Name = Path.GetFileName(SourceDirText),
                Label = Path.GetFileName(SourceDirText),
                Description = new DescriptionData()
                {
                    SourceDir = SourceDirText,
                    OutputDir = OutputDirText,
                    FileExt = ExtText.StartsWith(".") ? ExtText : $".{ExtText}",
                    FileCount = Directory.EnumerateFiles(SourceDirText, $"*{ExtText}").Count(),
                    State = ItemState.Awaiting,
                    Task = ItemTask.Copy,
                    LabelProgressType = ItemLabelProgressType.TotalFileCount,
                    ProgressBarType = ItemProgressBarType.File,
                },
                Background = Brushes.AliceBlue,
            });
        }
        private async Task AddTrim()
        {
            ObservableCollection<TrimData> trimData = new();
            DirectoryInfo dirInfo = new(SourceDirText);
            var files = dirInfo.EnumerateFiles("*" + ExtText).OrderBy(x => x.Name);
            foreach (var file in files)
            {
                trimData.Add(new TrimData(file));
        }
            TrimWindowViewModel trimWindowContext = new() { ListBoxItems = trimData };
            var result = await ShowTrimDialog.Handle(trimWindowContext);
            Trace.TraceInformation("Trim Dialog Result: " + result);
            if (result)
            {
                foreach (var item in trimData)
                {
                    Trace.TraceInformation("Name: " + item.FileInfo.FullName);
                    Trace.TraceInformation("Start Time: " + item.StartTime?.FormattedString);
                    Trace.TraceInformation("End Time: " + item.EndTime?.FormattedString);
                }
                TaskListItems.Add(new ListViewData()
                {
                    Name = Path.GetFileName(SourceDirText),
                    Label = Path.GetFileName(SourceDirText),
                    Description = new DescriptionData()
                    {
                        SourceDir = SourceDirText,
                        OutputDir = OutputDirText,
                        FileExt = ExtText.StartsWith('.') ? ExtText : $".{ExtText}",
                        TrimData = trimData,
                        FileCount = files.Count(),
                        Task = ItemTask.Trim,
                        LabelProgressType = ItemLabelProgressType.TotalFileCount,
                        ProgressBarType = ItemProgressBarType.File
                    },
                    Background = Brushes.BlanchedAlmond
                });
            }
        }
        private async Task StartQueueAsync(CancellationToken ct)
        {
            string response = await Task.Run(ProcessTaskItems);
            if (response != "0" && ct.IsCancellationRequested) //Queue stopped
            {
                Trace.TraceInformation($"Queue was canceled on file \"{response}\"");
                await ShowMessageBox.Handle(new MessageBoxParams
                {
                    Title = "Queue Canceled",
                    Message = $"Your queue was canceled on file {response}",
                    Buttons = MessageBoxButtons.Ok,
                    StartupLocation = WindowStartupLocation.CenterOwner
                });
            }
            else if (response == "0" && !ct.IsCancellationRequested) //Success
            {
                Trace.TraceInformation("Queue completed");
                await ShowMessageBox.Handle(new MessageBoxParams
                {
                    Title = "Queue Completed",
                    Message = "Your queue has finished",
                    Buttons = MessageBoxButtons.Ok,
                    StartupLocation = WindowStartupLocation.CenterOwner
                });
            }
            else
            {
                Trace.TraceError($"response = \"{response}\" token = \"{ct.IsCancellationRequested}\"");
                await ShowMessageBox.Handle(new MessageBoxParams
                {
                    Title = "Error",
                    Message = $"response = \"{response}\" token = \"{ct.IsCancellationRequested}\"",
                    Buttons = MessageBoxButtons.Ok,
                    StartupLocation = WindowStartupLocation.CenterOwner
                });
            }
        }
        private async Task<string> ProcessTaskItems()
        {
            string response = String.Empty;
            foreach (ListViewData item in TaskListItems)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    CurrentItemInProgress = item;
                    item.Description.State = ItemState.Progressing;
                });
                if (item.Description.Task == ItemTask.Transcode)
                {
                    FFmp = new FFmpeg(AppSettings.Settings.FFmpegPath);
                    string frameResult = FFmp.GetFrameCountApproximate(
                        dir: item.Description.SourceDir,
                        searchPattern: '*' + item.Description.FileExt,
                        args: item.Description.Profile.Arguments
                    );
                    Trace.TraceInformation(frameResult);
                    response = await FFmp.RunProfile(
                        args: item.Description.Profile.Arguments,
                        outputDir: item.Description.OutputDir,
                        ext: item.Description.Profile.OutputExtension,
                        progress: new Progress<double>(x => item.Progress = x),
                        viewModel: this
                    );
                }
                else if (item.Description.Task == ItemTask.Copy)
                {
                    Copier = new ProgressFileCopier(
                        progress: new Progress<double>(x => item.Progress = x),
                        item: item,
                        viewModel: this
                    );
                    response = await Copier.CopyDirectory(
                        sourceDir: item.Description.SourceDir,
                        outputDir: item.Description.OutputDir,
                        ext: '*' + item.Description.FileExt
                    );
                }
                else if (item.Description.Task == ItemTask.Trim)
                {
                    FFmp = new FFmpeg(AppSettings.Settings.FFmpegPath);
                    response = await FFmp.TrimDir(
                        sourceDir: item.Description.SourceDir,
                        outputDir: item.Description.OutputDir,
                        trimData: item.Description.TrimData!,
                        progress: new Progress<double>(x => item.Progress = x),
                        item: item,
                        viewModel: this
                    );
                }
                else
                {
                    throw new Exception("Internal Item error: ItemTask enum not properly assigned to Type property of Item Description property");
                }
                if (Int32.TryParse(response, out int result) && result == 0)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        item.Check = true;
                        item.Description.State = ItemState.Complete;
                    });
                }
                else if (response != String.Empty)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        item.Description.State = ItemState.Stopped;
                    });
                    break;
                }
                else
                {
                    throw new Exception($"Internal task response error: string not valid: \"{response}\"");
                }
            }
            CurrentItemInProgress = null;
            FFmp = null;
            Copier = null;
            return response;
        }
        private void StopQueue()
        {
            if (CurrentItemInProgress?.Description.Task == ItemTask.Transcode || CurrentItemInProgress?.Description.Task == ItemTask.Trim)
            {
                if (FFmp != null)
                {
                    FFmp.Stop();
                }
                else
                {
                    ShowMessageBox.Handle(new MessageBoxParams
                    {
                        Title = "Error",
                        Message = "There does not appear to be an FFmpeg instance running",
                        Buttons = MessageBoxButtons.Ok
                    });
                }
            }
            else if (CurrentItemInProgress?.Description.Task == ItemTask.Copy)
            {
                if (Copier != null)
                {
                    Copier.Stop();
                }
                else
                {
                    ShowMessageBox.Handle(new MessageBoxParams
                    {
                        Title = "Error",
                        Message = "There does not appear to be an copier instance running",
                        Buttons = MessageBoxButtons.Ok
                    });
                }
            }
            else { }
        }
        private async Task Editor(string controlName) //very little makes sense about this command (it's atrocious) but I felt like doing it this way just cause :) honestly this whole project is probably atrocious but I'm learning :)
        {
            string xml;
            if (controlName == nameof(Settings))
            {
                xml = AppSettings.GetXElementString<Settings>();
            }
            else if (controlName == nameof(Profile))
            {
                xml = AppSettings.GetXElementString<Profile>();
            }
            else return;
            string? result = await ShowTextEditorDialog.Handle(xml);
            if (result != null)
            {
                if (controlName == nameof(Settings))
                {
                    try
                    {
                        AppSettings.ImportSettingsXML(result);
                    }
                    catch
                    {
                        await ShowMessageBox.Handle(new MessageBoxParams
                        {
                            Title = "Error",
                            Message = "The xml could not be parsed",
                            Buttons = MessageBoxButtons.Ok,
                            StartupLocation = WindowStartupLocation.CenterOwner
                        });
                    }
                }
                else if (controlName == nameof(Profile))
                {
                    try
                    {
                        AppSettings.ImportProfilesXML(result); 
                    }
                    catch
                    {
                        await ShowMessageBox.Handle(new MessageBoxParams
                        {
                            Title = "Error",
                            Message = "The xml could not be parsed",
                            Buttons = MessageBoxButtons.Ok,
                            StartupLocation = WindowStartupLocation.CenterOwner
                        });
                    }
        }
            }
        }
        private async Task CheckForUpdates()
        {
            string assetIdentifier;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                assetIdentifier = "win";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                assetIdentifier = "linux";
            }
            else throw new Exception("OS Platform not supported");

            //catch exceptions
            var result = await Updater.CheckForUpdatesGitAsync("FFmpegAvalonia",
                assetIdentifier,
                "https://api.github.com/repos/Blitznir/FFmpegAvalonia/releases/latest",
                Assembly.GetExecutingAssembly().GetName().Version!.ToString(3),
                HttpClient!);

            if (result.UpdateAvailable)
            {
                var msgBoxResult = await ShowMessageBox.Handle(new MessageBoxParams
                {
                    Title = "Update Detected",
                    Header = $"Newer version {result.Version} found",
                    Message = "Would you like to update?",
                    Buttons = MessageBoxButtons.YesNo,
                    StartupLocation = WindowStartupLocation.CenterOwner
                });
                if (msgBoxResult == MessageBoxResult.Yes)
                {
                    //call updater with params
                    string updaterProcessPath = Path.Combine(AppContext.BaseDirectory, "CybertronUpdater.exe");
                    Cybertron.GenStatic.GetOSRespectiveExecutablePath(ref updaterProcessPath);
                    string thisProcessPath;
                    using (var thisProcess = Process.GetCurrentProcess())
                    {
                        thisProcessPath = thisProcess.MainModule.FileName;
                    }
                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = updaterProcessPath,
                    };
                    processStartInfo.ArgumentList.Add(result.DownloadLink);
                    Trace.TraceInformation(result.DownloadLink);
                    processStartInfo.ArgumentList.Add(AppContext.BaseDirectory);
                    Trace.TraceInformation(AppContext.BaseDirectory);
                    processStartInfo.ArgumentList.Add(thisProcessPath);
                    Trace.TraceInformation(thisProcessPath);
                    processStartInfo.ArgumentList.Add("profiles.xml");
                    processStartInfo.ArgumentList.Add("settings.xml");
                    Process.Start(processStartInfo);
                    Environment.Exit(1);
                }
            }
            else
            {
                await ShowMessageBox.Handle(new MessageBoxParams
                {
                    Title = "No Update Detected",
                    Message = "No new update detected",
                    Buttons = MessageBoxButtons.Ok,
                    StartupLocation = WindowStartupLocation.CenterOwner
                });
            }
        }
        private void OpenURL(string url)
        {
            
                }
        private readonly HttpClient _httpClient = new();
        public HttpClient HttpClient => _httpClient;
        private FFmpeg? _fFmp;
        public FFmpeg? FFmp
        {
            get => _fFmp;
            set => this.RaiseAndSetIfChanged(ref _fFmp, value);
            }
        private ProgressFileCopier? _copier;
        public ProgressFileCopier? Copier
        {
            get => _copier;
            set => this.RaiseAndSetIfChanged(ref _copier, value);
        }
        private ListViewData? _currentItemInProgress;
        public ListViewData? CurrentItemInProgress
        {
            get => _currentItemInProgress;
            set => this.RaiseAndSetIfChanged(ref _currentItemInProgress, value);
        }
        private bool _isQueueRunning = false;
        public bool IsQueueRunning
        {
            get => _isQueueRunning;
            set => this.RaiseAndSetIfChanged(ref _isQueueRunning, value);
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
        private string _extText = String.Empty;
        public string ExtText
        {
            get => _extText;
            set => this.RaiseAndSetIfChanged(ref _extText, value);
        }
        private readonly List<ItemTask> _taskTypeItems;
        public List<ItemTask> TaskTypeItems => _taskTypeItems;
        private ItemTask _selectedTaskType = ItemTask.Transcode;
        public ItemTask SelectedTaskType
        {
            get => _selectedTaskType;
            set => this.RaiseAndSetIfChanged(ref _selectedTaskType, value);
        }
        private readonly ObservableCollection<string> _profileItems;
        public ObservableCollection<string> ProfileItems => _profileItems;
        private Profile? _selectedProfile;
        public Profile? SelectedProfile
        {
            get => _selectedProfile;
            set => this.RaiseAndSetIfChanged(ref _selectedProfile, value);
        }
        private string _profileText = String.Empty;
        public string ProfileText
        {
            get => _profileText;
            set
            {
                this.RaiseAndSetIfChanged(ref _profileText, value);
                if (AppSettings.Profiles.TryGetValue(value, out Profile? profile))
                {
                    SelectedProfile = profile;
        }
                else if (SelectedProfile == null)
        {
                    return;
        }
                else
            {
                    SelectedProfile = null;
                }
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
        private readonly ObservableCollection<ListViewData> _taskListItems = new();
        public ObservableCollection<ListViewData> TaskListItems => _taskListItems;
    }
}
