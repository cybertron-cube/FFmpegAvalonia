using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit.TextMate;
using AvaloniaMessageBox;
using System;
using System.Collections.Generic;
using TextMateSharp.Grammars;
using System.ComponentModel;

namespace FFmpegAvalonia.Views
{
    public partial class TextEditorWindow : Window
    {
        private readonly TextMate.Installation TextMateInstallation;
        private bool CloseConfirmation;
        private bool TextChanged;
        public TextEditorWindow()
        {
            InitializeComponent();

            Editor.ContextMenu = new ContextMenu
            {
                Items = new List<MenuItem>
                {
                    new MenuItem { Header = "Copy", InputGesture = new KeyGesture(Key.C, KeyModifiers.Control) },
                    new MenuItem { Header = "Paste", InputGesture = new KeyGesture(Key.V, KeyModifiers.Control) },
                    new MenuItem { Header = "Cut", InputGesture = new KeyGesture(Key.X, KeyModifiers.Control) }
                }
            };

            var _registryOptions = new RegistryOptions(ThemeName.Monokai);
            TextMateInstallation = Editor.InstallTextMate(_registryOptions);
            TextMateInstallation.SetGrammar(_registryOptions.GetScopeByLanguageId(_registryOptions.GetLanguageByExtension(".xml").Id));

            Editor.TextArea.Caret.PositionChanged += Caret_PositionChanged;
            Opened += TextEditorWindow_Opened;

            AddHandler(PointerWheelChangedEvent, (o, i) =>
            {
                if (i.KeyModifiers != KeyModifiers.Control) return;
                if (i.Delta.Y > 0)
                {
                    if (Editor.FontSize < 60)
                    {
                        Editor.FontSize++;
                        FontSizeText.Text = $"Font Size: {Editor.FontSize}";
                    }
                }
                else
                {
                    if (Editor.FontSize > 10)
                    {
                        Editor.FontSize--;
                        FontSizeText.Text = $"Font Size: {Editor.FontSize}";
                    }
                }
            }, RoutingStrategies.Bubble, true);
        }
        private void CloseAndConfirm(string? resultOutput)
        {
            CloseConfirmation = true;
            Close(resultOutput);
        }
        private void TextEditorWindow_Opened(object? sender, EventArgs e)
        {
            Editor.TextChanged += Editor_TextChanged;
        }
        private void Editor_TextChanged(object? sender, EventArgs e)
        {
            TextChanged = true;
            Closing += TextEditorWindow_Closing;
            Editor.TextChanged -= Editor_TextChanged;
        }
        private async void TextEditorWindow_Closing(object? sender, CancelEventArgs e)
        {
            //This event is only subscribed to if text has been changed at least once so no need to check TextChanged boolean
            if (CloseConfirmation) return;
            e.Cancel = true;
            var msgBox = MessageBox.GetMessageBox(new MessageBoxParams
            {
                Title = "Editor Closing",
                Message = "Do you want to save your changes?",
                Buttons = MessageBoxButtons.YesNo,
                StartupLocation = WindowStartupLocation.CenterOwner
            });
            var result = await msgBox.ShowDialog(this);
            if (result == MessageBoxResult.Yes)
            {
                CloseAndConfirm(Editor.Text);
            }
            else if (result == MessageBoxResult.No)
            {
                CloseAndConfirm(null);
            }
        }
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            TextMateInstallation.Dispose();
        }
        private void SaveExit_Click(object? sender, RoutedEventArgs e)
        {
            if (TextChanged)
                CloseAndConfirm(Editor.Text);
            else
                CloseAndConfirm(null);
        }
        private void Caret_PositionChanged(object? sender, EventArgs e)
        {
            StatusText.Text = string.Format("Line {0} Column {1}",
                Editor.TextArea.Caret.Line,
                Editor.TextArea.Caret.Column);
        }
    }
}
