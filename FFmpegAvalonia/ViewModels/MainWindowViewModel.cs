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
            _taskTypeItems = Enum.GetValues(typeof(ItemTask)).Cast<ItemTask>().ToList();
            _profileItems = new ObservableCollection<string>(appSettings.Profiles.Keys);
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
                    x => x.ExtBoxIsEnabled,
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
            ShowTextEditorDialog = new Interaction<string, string?>();
            EditorCommand = ReactiveCommand.CreateFromTask<string>(Editor);
        public ReactiveCommand<string, Unit> EditorCommand { get; }
        public Interaction<string, string?> ShowTextEditorDialog;
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
        }
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
