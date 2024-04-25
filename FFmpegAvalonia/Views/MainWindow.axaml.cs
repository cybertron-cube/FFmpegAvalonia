using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;
using Avalonia.Layout;
using System.Reflection;
using System.Linq;
using FFmpegAvalonia.ViewModels;
using FFmpegAvalonia.AppSettingsX;
using Avalonia.VisualTree;
using ReactiveUI;
using Avalonia.ReactiveUI;
using FFmpegAvalonia.Views;
using System.Reactive.Linq;
using AvaloniaMessageBox;
using FFmpegAvalonia.Models;
using Serilog;
using Splat;
using ILogger = Serilog.ILogger;

namespace FFmpegAvalonia
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        public new MainWindowViewModel ViewModel;
        private readonly AppSettings _appSettings;
        private bool _confirmShutdown;
        private readonly ILogger _log = Log.ForContext<MainWindow>();
        
        public MainWindow()
        {
            InitializeComponent();
            _appSettings = Locator.Current.GetService<AppSettings>()!;
            ViewModel = new MainWindowViewModel(_appSettings);
            DataContext = ViewModel;
            this.WhenActivated(d =>
            {
                d(ViewModel!.ShowTextEditorDialog.RegisterHandler(DoShowTextEditorDialogAsync));
                d(ViewModel!.ShowTrimDialog.RegisterHandler(DoShowTrimDialogAsync));
                d(ViewModel!.ShowMessageBox.RegisterHandler(DoShowMessageBoxAsync));
            });
            this.Events().Closed.InvokeCommand(ViewModel.ExitAppCommand);
            AddHandler(DragDrop.DropEvent, Drop!);
            AddHandler(DragDrop.DragOverEvent, DragOver!);
            Closing += MainWindow_Closing;
            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
#if DEBUG
            Title = $"FFmpeg Avalonia Debug {currentVersion}";
#else
            if (currentVersion.Revision == 0 || currentVersion.Revision == null)
            {
                Title = $"FFmpeg Avalonia {currentVersion.ToString(3)}";
            }
            else
            {
                Title = $"FFmpeg Avalonia Dev Pre-Release {currentVersion}";
            }
#endif
        }
        
        private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_confirmShutdown) return;
            if (!ViewModel.IsQueueRunning) return;
            e.Cancel = true;
            var msgBox = MessageBox.GetMessageBox(new MessageBoxParams
            {
                Title = "Closing",
                Header = "The queue is still running",
                Message = "Are you sure you want to quit?",
                Buttons = MessageBoxButtons.YesNo,
                StartupLocation = WindowStartupLocation.CenterOwner
            });
            var result = await msgBox.ShowDialog(this);
            if (result != MessageBoxResult.Yes) return;
            _confirmShutdown = true;
            Close();
        }
        
        private async Task DoShowTextEditorDialogAsync(InteractionContext<string, string?> interaction)
        {
            var dialog = new TextEditorWindow();
            dialog.Editor.Text = interaction.Input;
            var result = await dialog.ShowDialog<string?>(this);
            interaction.SetOutput(result);
        }
        
        private async Task DoShowTrimDialogAsync(InteractionContext<TrimWindowViewModel, bool> interaction)
        {
            TrimWindow dialog = new()
            {
                DataContext = interaction.Input
            };
            var result = await dialog.ShowDialog<bool>(this);
            interaction.SetOutput(result);
        }
        
        private async Task DoShowMessageBoxAsync(InteractionContext<MessageBoxParams, MessageBoxResult> interaction)
        {
            var msgBox = MessageBox.GetMessageBox(interaction.Input);
            var result = await msgBox.ShowDialog(this);
            interaction.SetOutput(result);
        }
        
        private void DragOver(object sender, DragEventArgs e)
        {
            Debug.WriteLine("DragOver");
            // Only allow Copy or Link as Drop Operations.
            e.DragEffects = e.DragEffects & (DragDropEffects.Copy | DragDropEffects.Link);
            // Only allow if the dragged data contains text or filenames.
            if (!e.Data.Contains(DataFormats.Text) && !e.Data.Contains(DataFormats.FileNames))
                e.DragEffects = DragDropEffects.None;
        }
        
        private void Drop(object sender, DragEventArgs e)
        {
            Debug.WriteLine("Drop");
            Point pt = e.GetPosition((IVisual)sender);
            TextBox textBox;
            IInputElement inputElement = MainGrid.InputHitTest(pt)!;
            try
            {
                TextBlock textBlock = (TextBlock)inputElement.GetVisualDescendants().Where(x => x is TextBlock).Single();
                textBox = (TextBox)textBlock.TemplatedParent!;
            }
            catch (Exception ex)
            {
                _log.Error(ex.Message);
                return;
            }
            if (e.Data.Contains(DataFormats.Text))
                textBox.Text = e.Data.GetText();
            else if (e.Data.Contains(DataFormats.FileNames) && e.Data.GetFileNames()!.Count() == 1)
                textBox.Text = string.Join(Environment.NewLine, e.Data.GetFileNames()!);
        }
        
        private void Task_SelectionChanged(object? sender, SelectionChangedEventArgs e) //this can be done a better way
        {
            ItemTask itemTask = (ItemTask)e.AddedItems[0];

            if (itemTask == ItemTask.Transcode)
            {
                if (MainGrid.RowDefinitions[5].Height.Value == 67) return;
                else
                {
                    MainGrid.RowDefinitions[5].Height = new GridLength(67);
                    ProfileBox.IsEnabled = true;
                }
            }
            else
            {
                if (MainGrid.RowDefinitions[5].Height.Value != 0)
                {
                    MainGrid.RowDefinitions[5].Height = new GridLength(0);
                    ProfileBox.IsEnabled = false;
                }
            }

            if (itemTask == ItemTask.UploadAWS)
            {
                if (OutputLabel.Text == "s3://") return;
                else
                {
                    OutputLabel.Text = "s3://";
                    OutputDirBrowseBtn.IsEnabled = false;
                }
            }
            else
            {
                if (!OutputDirBrowseBtn.IsEnabled)
                {
                    OutputDirBrowseBtn.IsEnabled = true;
                }
                if (OutputLabel.Text != "Output Directory")
                {
                    OutputLabel.Text = "Output Directory";
                }
            }
        }
        
        private async void MainWindow_Opened(object? sender, EventArgs e)
        {
            _log.Information("Main Window Opened");
            ViewModel.SelectedTaskType = ItemTask.Copy; //fixes error popup not showing when switching to transcode
            ViewModel.AutoOverwriteCheck = _appSettings.Settings.AutoOverwriteCheck;
            if (_appSettings.Settings.FFmpegPath == String.Empty)
            {
                var msgBoxError = MessageBox.GetMessageBox(new MessageBoxParams
                {
                    Title = "Help!",
                    Message = "We could not find your ffmpeg path please select it",
                    Buttons = MessageBoxButtons.Ok,
                    StartupLocation = WindowStartupLocation.CenterOwner
                });
                var result = await msgBoxError.ShowDialog(this);
                if (result == MessageBoxResult.Ok)
                {
                    var dialog = new OpenFolderDialog();
                    var path = await dialog.ShowAsync(this);
                    if (path is not null)
                    {
                        _appSettings.Settings.FFmpegPath = path;
                    }
                    else
                    {
                        this.Close();
                    }
                }
                else
                {
                    this.Close();
                }
            }
            if (_appSettings.Settings.CheckUpdateOnStart)
            {
                _log.Information("CheckUpdateOnStart enabled");
                await ViewModel.CheckForUpdatesCommand.Execute(true);
            }
#if DEBUG
            var buttonGrid = this.FindControl<Grid>("ButtonSec");
            var testButton = new Button();
            buttonGrid.Children.Add(testButton);
            testButton.Content = "Test";
            testButton.HorizontalAlignment = HorizontalAlignment.Right;
            testButton.Click += Test_Click!;
#endif
        }
        
        private void MenuItemClose_Click(object? sender, RoutedEventArgs e)
        {
            this.Close();
        }
        
        private async void Browse_Click(object sender, RoutedEventArgs e)
        {
            var control = e.Source as Control;
            var dialog = new OpenFolderDialog();
            var result = await dialog.ShowAsync(this);
            if (result is not null)
            {
                var textBox = this.FindControl<TextBox>(Regex.Replace(control!.Name!, "Browse.*", "Box"));
                textBox.Text = result;
            }
        }
        
        private async void ListViewItem_Remove(object sender, RoutedEventArgs e) //BIND ENABLED INSTEAD
        {
            if (ViewModel.IsQueueRunning)
            {
                var msgBoxError = MessageBox.GetMessageBox(new MessageBoxParams
                {
                    Title = "Error",
                    Header = "The queue is currently running",
                    Message = "If you would like, you can stop the queue",
                    Buttons = MessageBoxButtons.Ok
                });
                await msgBoxError.ShowDialog(this);
            }
            else
            {
                Control control = (Control)sender;
                ListViewData data = (ListViewData)control.DataContext!;
                ViewModel.TaskListItems.Remove(data);
            }
        }
        
#if DEBUG
        private void Test_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SourceDirText = @"G:\OBS\10 Transfer Orders\test";
            ViewModel.OutputDirText = @"G:\OBS\10 Transfer Orders\test\out";
            ViewModel.ExtText = "mkv";
            ViewModel.SelectedTaskType = ItemTask.Trim;
            Debug.WriteLine("TEST");
        }
#endif
        
        private async void ListViewItem_Edit(object sender, RoutedEventArgs e) //BIND ENABLED INSTEAD
        {
            if (ViewModel.IsQueueRunning) { return; }
            Control control = (Control)sender;
            ListViewData data = (ListViewData)control.DataContext!;
            if (data.Description.Task == ItemTask.Trim)
            {
                ObservableCollection<TrimData> newCollection = new();
                foreach (TrimData trimData in data.Description.TrimData!)
                {
                    TrimData trim = new(trimData.FileInfo)
                    {
                        StartTime = trimData.StartTime,
                        EndTime = trimData.EndTime
                    };
                    newCollection.Add(trim);
                }
                TrimWindow trimWindow = new() { DataContext = new TrimWindowViewModel() { ListBoxItems = newCollection } };
                bool result = await trimWindow.ShowDialog<bool>(this);
                if (result)
                {
                    data.Description.TrimData = newCollection;
                    foreach (var item in data.Description.TrimData!)
                    {
                        _log.Information("Name: " + item.FileInfo.FullName);
                        _log.Information("Start Time: " + item.StartTime?.FormattedString);
                        _log.Information("End Time: " + item.EndTime?.FormattedString);
                    }
                }
            }
        }
    }
}
