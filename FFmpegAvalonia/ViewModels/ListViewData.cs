using ReactiveUI;
using System;

namespace FFmpegAvalonia.ViewModels
{
    public class ListViewData : ReactiveObject
    {
        private string _name = String.Empty;
        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }
        private string _label = String.Empty;
        public string Label
        {
            get => _label;
            set => this.RaiseAndSetIfChanged(ref _label, value);
        }
        private DescriptionData _description = new();
        public DescriptionData Description
        {
            get => _description;
            set => this.RaiseAndSetIfChanged(ref _description, value);
        }
        private bool _check;
        public bool Check
        {
            get => _check;
            set => this.RaiseAndSetIfChanged(ref _check, value);
        }
        private double _progress;
        public double Progress
        {
            get => _progress;
            set => this.RaiseAndSetIfChanged(ref _progress, value);
        }
        private Avalonia.Media.IBrush _background = Avalonia.Media.Brushes.Transparent;
        public Avalonia.Media.IBrush Background
        {
            get => _background;
            set => this.RaiseAndSetIfChanged(ref _background, value);
        }
    }
}
