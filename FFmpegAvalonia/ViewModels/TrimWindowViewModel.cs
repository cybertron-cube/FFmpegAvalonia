using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace FFmpegAvalonia.ViewModels
{
    public class TrimWindowViewModel : ReactiveObject
    {
        public TrimWindowViewModel()
        {
            IObservable<bool> canExecute =
                this.WhenAnyValue(
                    x => x.StartTime,
                    x => x.EndTime,
                    x => x.ListBoxSelectedItem,
                    (startTime, endTime, listBoxSelectedItem) => (startTime.Length == TextMaxLength || startTime == "0")
                                                                 && (endTime.Length == TextMaxLength || endTime == "0")
                                                                 && listBoxSelectedItem is not null);
            SetTimeCodeValues = ReactiveCommand.Create(() =>
            {
                ListBoxSelectedItem!.StartTime = StartTime;
                ListBoxSelectedItem!.EndTime = EndTime;
            }, canExecute);
        }
        public ReactiveCommand<Unit, Unit> SetTimeCodeValues { get; }
        private const int _textMaxLength = 12;
        public static int TextMaxLength { get { return _textMaxLength; } }
        private ObservableCollection<TrimData>? _listBoxItems;
        public ObservableCollection<TrimData>? ListBoxItems
        {
            get => _listBoxItems;
            set => this.RaiseAndSetIfChanged(ref _listBoxItems, value);
        }
        private TrimData? _listBoxSelectedItem;
        public TrimData? ListBoxSelectedItem
        {
            get => _listBoxSelectedItem;
            set => this.RaiseAndSetIfChanged(ref _listBoxSelectedItem, value);
        }
        private string _startTime = String.Empty;
        public string StartTime
        {
            get => _startTime;
            set => this.RaiseAndSetIfChanged(ref _startTime, value);
        }
        private string _endTime = String.Empty;
        public string EndTime
        {
            get => _endTime;
            set => this.RaiseAndSetIfChanged(ref _endTime, value);
        }
    }
}
