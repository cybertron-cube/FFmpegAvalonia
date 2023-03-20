using Avalonia.Controls;
using Avalonia.Input;
using FFmpegAvalonia.ViewModels;
using System.Collections.ObjectModel;
using Avalonia.Interactivity;
using ExtensionMethods;
using PCLUntils.IEnumerables;
using System.Linq;
using System;
using System.Diagnostics;

namespace FFmpegAvalonia.Views
{
    public partial class TrimWindow : Window
    {
        public TrimWindow()
        {
            InitializeComponent();
            StartTimeCodeTextBox.AddHandler(TextInputEvent, TextInputValidation, RoutingStrategies.Tunnel);
            EndTimeCodeTextBox.AddHandler(TextInputEvent, TextInputValidation, RoutingStrategies.Tunnel);
            TimeCodeListBox.SelectionChanged += TimeCodeListBox_SelectionChanged;
        }
        private void TimeCodeListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            StartTimeCodeTextBox.Focus();
            StartTimeCodeTextBox.SelectAll();
        }
        private void TextInputValidation(object? sender, TextInputEventArgs e)
        {
            TextBox textBox = (TextBox)sender!;
            int textLength = textBox.Text.Count(x => !x.Equals(':') && !x.Equals('.'));
            if (Int32.TryParse(e.Text, out _))
            {
                if (textLength > 0 && textLength < 6 && textLength.IsEven() && !textBox.Text.EndsWith(":"))
                {
                    e.Text = ":" + e.Text;
                    return;
                }
                else if (textLength == 6 && !textBox.Text.EndsWith("."))
                {
                    e.Text = "." + e.Text;
                    return;
                }
                else if (textBox.Text.Length == textBox.MaxLength - 1)
                {
                    if (textBox.Name == "StartTimeCodeTextBox")
                    {
                        EndTimeCodeTextBox.Focus();
                        EndTimeCodeTextBox.SelectAll();
                    }
                    else SetTimeCodeBtn.Focus();
                    return;
                }
                else return;
            }
            else if (e.Text == ":")
            {
                if (textLength > 0 && textLength < 6 && textLength.IsEven() && !textBox.Text.EndsWith(":"))
                {
                    return;
                }
                else
                {
                    e.Handled = true;
                    return;
                }
            }
            else if (e.Text == ".")
            {
                if (textLength == 6 && !textBox.Text.EndsWith("."))
                {
                    return;
                }
                else
                {
                    e.Handled = true;
                    return;
                }
            }
            else
            {
                e.Handled = true;
            }
        }
    }
}