using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AvaloniaEdit;
using ExtensionMethods;

namespace FFmpegAvalonia
{
    public class TimeCode
    {
        private static readonly Regex _timeCodeRegex = new("(?<hours>\\d\\d):(?<minutes>\\d\\d):(?<seconds>\\d\\d).(?<milliseconds>\\d+)");
        private short _hours;
        public short Hours
        {
            get { return _hours; }
            set
            {
                if (value >= 0 && value < 100)
                {
                    _hours = value;
                    UpdateFormattedStringAndValue();
                }
                else throw new ArgumentOutOfRangeException(nameof(value), value.ToString());
            }
        }
        private short _minutes;
        public short Minutes
        {
            get { return _minutes; }
            set
            {
                if (value >= 0 && value < 60)
                {
                    _minutes = value;
                    UpdateFormattedStringAndValue();
                }
                else throw new ArgumentOutOfRangeException(nameof(value), value.ToString());
            }
        }
        private short _seconds;
        public short Seconds
        {
            get { return _seconds; }
            set
            {
                if (value >= 0 && value < 60)
                {
                    _seconds = value;
                    UpdateFormattedStringAndValue();
                }
                else throw new ArgumentOutOfRangeException(nameof(value), value.ToString());
            }
        }
        private short _milliseconds;
        public short Milliseconds
        {
            get { return _milliseconds; }
            set
            {
                if (value >= 0 && value < 1000)
                {
                    _milliseconds = value;
                    UpdateFormattedStringAndValue();
                }
                else throw new ArgumentOutOfRangeException(nameof(value), value.ToString());
            }
        }
        private int _value;
        public int Value { get { return _value; } }
        private string _formattedString;
        public string FormattedString { get { return _formattedString; } }
        public TimeCode(short hours, short minutes, short seconds, short milliseconds)
        {
            _hours = hours;
            _minutes = minutes;
            _seconds = seconds;
            _milliseconds = milliseconds;
            _formattedString = String.Format("{0}:{1}:{2}.{3}",
                PadTimeCodeUnit(hours),
                PadTimeCodeUnit(minutes),
                PadTimeCodeUnit(seconds),
                PadTimeCodeUnit(milliseconds, 3));
            _value = ToInt(_formattedString);
        }
        public static TimeCode Parse(string text)
        {
            var match = _timeCodeRegex.Match(text);
            if (match.Success)
            {
                TimeCode timeCode = new(
                    Int16.Parse(match.Groups["hours"].Value),
                    Int16.Parse(match.Groups["minutes"].Value),
                    Int16.Parse(match.Groups["seconds"].Value),
                    Int16.Parse(match.Groups["milliseconds"].Value));
                return timeCode;
            }
            else throw new FormatException("Input string was not in a correct format.");
        }
        public static string TimeSpan(TimeCode timeCodeOne, TimeCode timeCodeTwo)
        {
            throw new NotImplementedException();
        }
        public static int ToInt(string str)
        {
            return Int32.Parse(str.Split(':', '.').Combine());
        }
        public static double ToDouble(string str)
        {
            return double.Parse(str.Split(':', '.').Combine());
        }
        public static string PadTimeCodeUnit(int unit, int places = 2)
        {
            return unit.ToString().PadLeft(places, '0');
        }
        private void UpdateFormattedStringAndValue()
        {
            _formattedString = String.Format("{0}:{1}:{2}.{3}",
                PadTimeCodeUnit(_hours),
                PadTimeCodeUnit(_minutes),
                PadTimeCodeUnit(_seconds),
                PadTimeCodeUnit(_milliseconds, 3));
            _value = ToInt(_formattedString);
        }
    }
}
