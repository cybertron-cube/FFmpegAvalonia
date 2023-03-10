using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static FFmpegAvalonia.MainWindow;

namespace FFmpegAvalonia.ViewModels
{
    internal class ListViewData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private string _Name = string.Empty;
        public string Name
        {
            get { return _Name; }
            set
            {
                if (_Name != value)
                {
                    _Name = value;
                    RaisePropertyChanged(nameof(Name));
                }
            }
        }
        private readonly object LabelLock = new();
        private string _Label = string.Empty;
        public string Label
        {
            get { return _Label; }
            set
            {
                if (_Label != value)
                {
                    lock (LabelLock)
                    {
                        _Label = value;
                        RaisePropertyChanged(nameof(Label));
                    }
                }
            }
        }
        private DescriptionData _Description = new();
        public DescriptionData Description
        {
            get { return _Description; }
            set
            {
                if (_Description != value)
                {
                    _Description = value;
                    RaisePropertyChanged(nameof(Description));
                }
            }
        }
        private bool _Check;
        public bool Check
        {
            get { return _Check; }
            set
            {
                if (_Check != value)
                {
                    _Check = value;
                    RaisePropertyChanged(nameof(Check));
                }
            }
        }
        private double _Progress;
        public double Progress
        {
            get { return _Progress; }
            set
            {
                if (_Progress != value)
                {
                    _Progress = value;
                    RaisePropertyChanged(nameof(Progress));
                }
            }
        }
        private Avalonia.Media.IBrush _Background = Avalonia.Media.Brushes.Transparent;
        public Avalonia.Media.IBrush Background
        {
            get { return _Background; }
            set
            {
                if (value != _Background)
                {
                    _Background = value;
                    RaisePropertyChanged(nameof(Background));
                }
            }
        }
    }
}
