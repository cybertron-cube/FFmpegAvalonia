using Avalonia.Data;
using Avalonia.Data.Converters;
using FFmpegAvalonia.AppSettingsX;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFmpegAvalonia.ValueConverters
{
    public class ProfileConverter : IValueConverter
    {
        public static readonly ProfileConverter Instance = new();
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Profile profile)
            {
                return profile.Name;
            }
            else return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
