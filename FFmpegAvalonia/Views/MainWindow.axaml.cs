using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;
using System.IO;
using Path = System.IO.Path;
using Avalonia.Layout;
using PCLUntils.IEnumerables;
using System.Reflection;
using System.Linq;
using FFmpegAvalonia.ViewModels;
using FFmpegAvalonia.AppSettingsX;
using Avalonia.VisualTree;
using ReactiveUI;
using Avalonia.ReactiveUI;
using FFmpegAvalonia.Views;
using System.Reactive.Linq;

namespace FFmpegAvalonia
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        public AppSettings AppSettings = new();
        public new MainWindowViewModel ViewModel;
        private bool _confirmShutdown;
        public MainWindow()
        {
            FileInfo logPath = new(Path.Combine(AppContext.BaseDirectory, "debug.log"));
            if (logPath.Exists && logPath.Length > 20971520)
            {
                logPath.Delete();
            }
            TextWriterTraceListener textWriter = new(logPath.FullName);
            ConsoleTraceListener listener = new()
            {
                Writer = textWriter.Writer,
                TraceOutputOptions = TraceOptions.DateTime
            };
            Trace.Listeners.Add(listener);
            Trace.AutoFlush = true;
            InitializeComponent();
            ViewModel = new(AppSettings);
            DataContext = ViewModel;
            this.WhenActivated(d => d(ViewModel!.ShowTextEditorDialog.RegisterHandler(DoShowTextEditorDialogAsync))); //COMBINE these into one line????
            this.WhenActivated(d => d(ViewModel!.ShowTrimDialog.RegisterHandler(DoShowTrimDialogAsync)));//
            //this.WhenActivated(d => d(ViewModel!.ShowDownloadUpdatesDialog.RegisterHandler(DoShowDownloadUpdatesDialogAsync)));
            this.WhenActivated(d => d(ViewModel!.ShowMessageBox.RegisterHandler(DoShowMessageBoxAsync)));//
            AddHandler(DragDrop.DropEvent, Drop!);
            AddHandler(DragDrop.DragOverEvent, DragOver!);
            Closing += MainWindow_Closing;
#if DEBUG
            Title = $"FFmpeg Avalonia Debug {Assembly.GetExecutingAssembly().GetName().Version}";
#else
            Title = $"FFmpeg Avalonia {Assembly.GetExecutingAssembly().GetName().Version!.ToString(3)}";
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
            if (result == MessageBoxResult.Yes)
            {
                _confirmShutdown = true;
                this.Close();
            }
        }

        private async Task DoShowTextEditorDialogAsync(InteractionContext<string, string?> interaction)
        {
            var dialog = new TextEditorWindow();
            dialog.Editor.Text = interaction.Input;
            var result = await dialog.ShowDialog<string?>(this);
            interaction.SetOutput(result);
        }
        /*private async Task DoShowDownloadUpdatesDialogAsync(InteractionContext<Updater.CheckUpdateResult, Unit> interaction)
        {
            var dialog = new DownloadUpdateWindow();
            dialog.CheckUpdateResult = interaction.Input;
            dialog.HttpClient = ViewModel.HttpClient;
            await dialog.ShowDialog(this);
            interaction.SetOutput(new Unit());
        }*/
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
                //TextBlock textBlock = (TextBlock)inputElement.GetVisualChildren().ToList()[0].GetVisualChildren().ToList()[0];
                textBox = (TextBox)textBlock.TemplatedParent!;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message);
                return;
            }
            if (e.Data.Contains(DataFormats.Text))
                textBox.Text = e.Data.GetText();
            else if (e.Data.Contains(DataFormats.FileNames) && e.Data.GetFileNames()!.Count() == 1)
                textBox.Text = string.Join(Environment.NewLine, e.Data.GetFileNames()!);
        }
        private async void MainWindow_Opened(object? sender, EventArgs e)
        {
            Trace.TraceInformation("Main Window Opened");
            ViewModel.SelectedTaskType = ItemTask.Copy; //fixes error popup not showing when switching to transcode
            if (AppSettings.Settings.FFmpegPath == String.Empty)
            {
                var msgBoxError = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow("Help!", $"We could not find your ffmpeg path please select it", ButtonEnum.Ok);
                var result = await msgBoxError.ShowDialog(this);
                if (result == ButtonResult.Ok)
                {
                    var dialog = new OpenFolderDialog();
                    var path = await dialog.ShowAsync(this);
                    if (path is not null)
                    {
                        AppSettings.Settings.FFmpegPath = path;
                    }
                }
                else
                {
                    this.Close();
                }
            }
            if (AppSettings.Profiles.Count > 0)
            {
                foreach (var profile in AppSettings.Profiles.Keys)
                {
                    ProfileBoxItems.Add(profile);
                }
                ProfileBox.Items = ProfileBoxItems;
            }
            ViewModel.AutoOverwriteCheck = AppSettings.Settings.AutoOverwriteCheck;
#if DEBUG
            var buttonGrid = this.FindControl<Grid>("ButtonSec");
            var testButton = new Button();
            buttonGrid.Children.Add(testButton);
            testButton.Content = "Test";
            testButton.HorizontalAlignment = HorizontalAlignment.Right;
            testButton.Click += Test_Click!;
#endif
        }
        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            Trace.TraceInformation("Stopping FFmpeg process if running...");
            ViewModel.FFmp?.Stop();
            Trace.TraceInformation("Stopping copier instance if running...");
            ViewModel.Copier?.Stop();
            Trace.TraceInformation("Saving settings...");
            AppSettings.Settings.AutoOverwriteCheck = ViewModel.AutoOverwriteCheck;
            AppSettings.Save();
            Trace.TraceInformation("Exiting...");
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
                var msgBoxError = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(new MessageBox.Avalonia.DTO.MessageBoxStandardParams
                {
                    ButtonDefinitions = ButtonEnum.Ok,
                    ContentTitle = "Error",
                    ContentHeader = "The queue is currently running",
                    ContentMessage = "If you would like, you can stop the queue"
                });
                await msgBoxError.ShowDialog(this);
                return;
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

            /*
             * aws s3 cp /video/ s3://ss-texas/video/ --exclude “*” –include “*.mp4” –recursive
             * progress?
             * 
             * release?
             * 
             * details panel
             * hash
             * 3 log files
             * 
             * use command for listviewitem_remove
             * task_listselectionchanged improvement
             * stopqueue only 2 lines ffmp?.stop and copier?.stop
             * fully implement cancellation token
             */
        }
#endif
        private async void ListViewItem_Edit(object sender, RoutedEventArgs e) //BIND ENABLED INSTEAD
        {
            if (ViewModel.IsQueueRunning) { return; }
            Control control = (Control)sender;
            ListViewData data = (ListViewData)control.DataContext!;
            if (data.Description.Task == ItemTask.Trim) 
            {
                TrimWindow trimWindow = new() { DataContext = new TrimWindowViewModel() { ListBoxItems = data.Description.TrimData } };
                await trimWindow.ShowDialog(this);
                foreach (var item in data.Description.TrimData!)
                {
                    Trace.TraceInformation("Name: " + item.FileInfo.FullName);
                    Trace.TraceInformation("Start Time: " + item.StartTime?.FormattedString);
                    Trace.TraceInformation("End Time: " + item.EndTime?.FormattedString);
                }
            }
        }
        private async void AddToQueue_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.ValidationContext.IsValid)
            {
                goto MsgBoxError;
            }
            if (CopySourceCheck.IsChecked.NullIsFalse())
            {
                ListViewItems.Add(new ListViewData()
                {
                    Name = Path.GetFileName(SourceDirBox.Text),
                    Label = Path.GetFileName(SourceDirBox.Text),
                    Description = new DescriptionData()
                    {
                    Trace.TraceInformation("Name: " + item.FileInfo.FullName);
                    Trace.TraceInformation("Start Time: " + item.StartTime?.FormattedString);
                    Trace.TraceInformation("End Time: " + item.EndTime?.FormattedString);
                }
                }
            }
        }
    }
}
