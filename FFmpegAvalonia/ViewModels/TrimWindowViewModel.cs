using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Reactive;

namespace FFmpegAvalonia.ViewModels
{
    public class TrimWindowViewModel : ReactiveObject
    {
        public TrimWindowViewModel()
        {
            IObservable<bool> setCanExecute =
                this.WhenAnyValue(
                    x => x.StartTime,
                    x => x.EndTime,
                    x => x.ListBoxSelectedItem,
                    (startTime, endTime, listBoxSelectedItem) => (TimeCode.TryParseToInt(endTime) > TimeCode.TryParseToInt(startTime) || (startTime == "0" && TimeCode.TryParseToInt(endTime) > 0))
                                                                 && listBoxSelectedItem is not null);
            IObservable<bool> removeCanExecute =
                this.WhenAnyValue(
                    property1: x => x.ListBoxSelectedItem,
                    selector: (listBoxSelectedItem) => listBoxSelectedItem is not null
                                                       && listBoxSelectedItem.StartTime is not null);
            SetTimeCodeValues = ReactiveCommand.Create(() =>
            {
                ListBoxSelectedItem!.StartTime = StartTime == "0" ? TimeCode.Parse("00:00:00.000") : TimeCode.Parse(StartTime);
                ListBoxSelectedItem!.EndTime = TimeCode.Parse(EndTime);
            }, setCanExecute);
            RemoveTimeCodeValues = ReactiveCommand.Create(() =>
            {
                ListBoxSelectedItem!.StartTime = null;
                ListBoxSelectedItem!.EndTime = null;
            }, removeCanExecute);
            SaveExit = ReactiveCommand.Create<object?>(() =>
            {
                return true;
            });
        }
        public ReactiveCommand<Unit, Unit> SetTimeCodeValues { get; }
        public ReactiveCommand<Unit, Unit> RemoveTimeCodeValues { get; }
        public ReactiveCommand<Unit, object?> SaveExit { get; }
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
