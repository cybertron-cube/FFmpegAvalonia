using Avalonia.Controls;
using Avalonia.Input;
using FFmpegAvalonia.ViewModels;
using Avalonia.Interactivity;
using ExtensionMethods;
using PCLUntils.IEnumerables;
using System.Linq;
using System;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Avalonia;
using System.Diagnostics;
using System.Text;

namespace FFmpegAvalonia.Views
{
    public partial class TrimWindow : ReactiveWindow<TrimWindowViewModel>
    {
        public TrimWindow()
        {
            InitializeComponent();
            StartTimeCodeTextBox.AddHandler(TextInputEvent, TextInputValidation, RoutingStrategies.Tunnel);
            EndTimeCodeTextBox.AddHandler(TextInputEvent, TextInputValidation, RoutingStrategies.Tunnel);
            StartTimeCodeTextBox.PastingFromClipboard += TimeCodeTextBox_PastingFromClipboard;
            EndTimeCodeTextBox.PastingFromClipboard += TimeCodeTextBox_PastingFromClipboard;
            TimeCodeListBox.SelectionChanged += TimeCodeListBox_SelectionChanged;
            Opened += TrimWindow_Opened;
            this.WhenActivated(d => d(ViewModel!.SaveExit.Subscribe(Close)));
        }
        private async void TimeCodeTextBox_PastingFromClipboard(object? sender, RoutedEventArgs e)
        {
            TextBox textBox = (TextBox)e.Source!;
            string pasteText = await Application.Current!.Clipboard!.GetTextAsync();
            if (pasteText != null && ValidateChar(ref pasteText, textBox, out string manual))
            {
                Trace.TraceInformation($"Pasting \"{pasteText}\" into {textBox.Name}");
                if (manual != String.Empty)/////////////////////////////////////////////////////////////////////////////////
                {
                    textBox.Text = manual;
                    textBox.CaretIndex = textBox.Text.Length;
                    e.Handled = true;
                    return;
                }
                return;
            }
            e.Handled = true;
        }
        private static bool ValidateChar(ref string inputText, TextBox textBox, out string manual)
        {
            manual = String.Empty;
            Debug.WriteLine(textBox.CaretIndex);
            int selectionLength = Math.Abs(textBox.SelectionEnd - textBox.SelectionStart);

            if (selectionLength == 0 && inputText.Length > (TrimWindowViewModel.TextMaxLength - textBox.Text.Length))
            {
                return false;
            }

            string combine;

            if (selectionLength > 0)
            {
                int selectionStart;
                int selectionEnd;
                if (textBox.SelectionStart < textBox.SelectionEnd)
                {
                    selectionStart = textBox.SelectionStart;
                    selectionEnd = textBox.SelectionEnd;
                }
                else
                {
                    selectionStart = textBox.SelectionEnd;
                    selectionEnd = textBox.SelectionStart;
                }
                combine = String.Concat(textBox.Text.AsSpan(0, selectionStart), inputText, textBox.Text.AsSpan(selectionEnd, textBox.Text.Length - selectionEnd));
                if (combine.Length > TrimWindowViewModel.TextMaxLength)
                {
                    return false;
                }
            }
            else if (textBox.CaretIndex == 0)
            {
                combine = inputText + textBox.Text;
            }
            else if (textBox.CaretIndex == textBox.Text.Length)
            {
                combine = textBox.Text + inputText;
            }
            else //caretindex located within the textbox text
            {
                combine = String.Concat(textBox.Text.AsSpan(0, textBox.CaretIndex), inputText, textBox.Text.AsSpan(textBox.CaretIndex, textBox.Text.Length - textBox.CaretIndex));
            }
            Debug.WriteLine(combine);
            if (combine.Length == TrimWindowViewModel.TextMaxLength && TimeCode.TryParse(combine, out _))
            {
                return true;
            }
            int index = 0;
            int placeholder = 2;
            StringBuilder manualSB = new();
            foreach (char c in combine)
            {
                if (index == placeholder || index == placeholder + 3)
                {
                    if (c.Equals(':'))
                    {
                        manualSB.Append(c);
                    }
                    else
                    {
                        manualSB.Append(':');
                        manualSB.Append(c);
                        placeholder -= 1;
                    }
                }
                else if (index == placeholder + 6)
                {
                    if (c.Equals('.'))
                    {
                        manualSB.Append(c);
                    }
                    else
                    {
                        manualSB.Append('.');
                        manualSB.Append(c);
                    }
                }
                else if (Int16.TryParse(c.ToString(), out _))
                {
                    manualSB.Append(c);
                }
                else
                {
                    return false;
                }
                index++;
            }
            manual = manualSB.ToString();
            return true;
        }
        private void TrimWindow_Opened(object? sender, EventArgs e)
        {
            TimeCodeListBox.SelectedItem = TimeCodeListBox.Items.ElementAt(0);
        }
        private void TimeCodeListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            StartTimeCodeTextBox.Focus();
        }
        private void TextInputValidation(object? sender, TextInputEventArgs e)
        {
            TextBox textBox = (TextBox)sender!;
            int selectionLength = Math.Abs(textBox.SelectionEnd - textBox.SelectionStart);
            if (selectionLength == 0)
            {
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
            else
            {
                int selectionStart;
                int selectionEnd;
                if (textBox.SelectionStart < textBox.SelectionEnd)
                {
                    selectionStart = textBox.SelectionStart;
                    selectionEnd = textBox.SelectionEnd;
                }
                else
                {
                    selectionStart = textBox.SelectionEnd;
                    selectionEnd = textBox.SelectionStart;
                }
                string combine = String.Concat(textBox.Text.AsSpan(0, selectionStart), e.Text, textBox.Text.AsSpan(selectionEnd, textBox.Text.Length - selectionEnd));
                if (combine.Length > TrimWindowViewModel.TextMaxLength)
                {
                    e.Handled = true;
                    return;
                }
                string text = String.Join("", combine.Where(x => !x.Equals(':') && !x.Equals('.')));
                int ti = 0;
                StringBuilder sb = new(12);
                for (int i = 0; i < 12; i++)
                {
                    if (ti > text.Length - 1)
                    {
                        e.Handled = true;
                        textBox.Text = sb.ToString();
                        textBox.CaretIndex = textBox.Text.Length;
                        textBox.ClearSelection();
                        return;
                    }
                    if (sb.Length < 8)
                    {
                        if (sb.Length == 2 || sb.Length == 5)
                        {
                            sb.Append(':');
                        }
                        else
                        {
                            if (Int32.TryParse(text[ti].ToString(), out _))
                            {
                                sb.Append(text[ti]);
                                ti++;
                            }
                            else
                            {
                                e.Handled = true;
                                return;
                            }
                        }
                    }
                    else
                    {
                        if (sb.Length == 8)
                        {
                            sb.Append('.');
                        }
                        else
                        {
                            if (Int32.TryParse(text[ti].ToString(), out _))
                            {
                                sb.Append(text[ti]);
                                ti++;
                            }
                            else
                            {
                                e.Handled = true;
                                return;
                            }
                        }
                    }
                }
            }
        }
    }
}