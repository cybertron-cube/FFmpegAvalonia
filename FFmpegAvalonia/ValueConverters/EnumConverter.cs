using Avalonia.Data.Converters;
using Avalonia.Data;
using System;
using System.Globalization;

namespace FFmpegAvalonia.ValueConverters
{
    public class EnumConverter : IValueConverter
    {
        public static readonly EnumConverter Instance = new();
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not null && Enum.IsDefined(typeof(ItemTask), value))
            {
                return ((ItemTask)value).ToString();
            }
            else return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
