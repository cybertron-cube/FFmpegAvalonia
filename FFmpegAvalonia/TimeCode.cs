using System;
using System.Text.RegularExpressions;
using ExtensionMethods;

namespace FFmpegAvalonia
{
    /// <summary>
    /// Represents a timecode consisting of hours, minutes, seconds, milliseconds
    /// </summary>
    public class TimeCode
    {
        public static readonly Regex TimeCodeRegex = new("^(?<hours>\\d\\d):(?<minutes>\\d\\d):(?<seconds>\\d\\d).(?<milliseconds>\\d\\d\\d)$");
        private short _hours;
        /// <summary>
        /// Hours unit of the timecode
        /// </summary>
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
        /// <summary>
        /// Minutes unit of the timecode
        /// </summary>
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
        /// <summary>
        /// Seconds unit of the timecode
        /// </summary>
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
        /// <summary>
        /// Milliseconds unit of the timecode
        /// </summary>
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
        /// <summary>
        /// Integer of the entire timecode used to compare to other timecodes
        /// </summary>
        public int Value { get { return _value; } }
        private string _formattedString;
        /// <summary>
        /// Timecode represented in a properly formatted string (hh:mm:ss:msmsms)
        /// </summary>
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
        public double GetTotalSeconds()
        {
            return _hours * 3600 + _minutes * 60 + _seconds + (double)_milliseconds / 1000;
        }
        public static double GetTotalSeconds(string timecode)
        {
            return Convert.ToInt16(timecode[0..2]) * 3600
                + Convert.ToInt16(timecode[3..5]) * 60
                + Convert.ToDouble(timecode[6..12]);
        }
        /// <summary>
        /// Creates a new <see cref="TimeCode"/> object from a string
        /// </summary>
        /// <param name="text">A string that contains a timecode represented in hh:mm:ss:msmsms</param>
        /// <returns>A <see cref="TimeCode"/> object from a valid string</returns>
        /// <exception cref="FormatException">Timecode string was not represented in hh:mm:ss:msmsms</exception>
        public static TimeCode Parse(string text)
        {
            Match match = TimeCodeRegex.Match(text);
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
        /// <summary>
        /// Creates a new <see cref="TimeCode"/> object from a string/>
        /// </summary>
        /// <param name="text">A string that contains a timecode represented in hh:mm:ss:msmsms</param>
        /// <param name="timeCode">The <see cref="TimeCode"/> representation of the text</param>
        /// <returns>A boolean indicating whether the string is able to be parsed</returns>
        public static bool TryParse(string text, out TimeCode? timeCode)
        {
            Match match = TimeCodeRegex.Match(text);
            if (match.Success)
            {
                timeCode = new(
                    Int16.Parse(match.Groups["hours"].Value),
                    Int16.Parse(match.Groups["minutes"].Value),
                    Int16.Parse(match.Groups["seconds"].Value),
                    Int16.Parse(match.Groups["milliseconds"].Value));
                return true;
            }
            else
            {
                timeCode = null;
                return false;
            }
        }
        /// <summary>
        /// Calculates the difference between two <see cref="TimeCode"/> objects
        /// </summary>
        /// <param name="timeCode1">First <see cref="TimeCode"/> object</param>
        /// <param name="timeCode2">Second <see cref="TimeCode"/> object</param>
        /// <returns>A <see cref="TimeCode"/> object that represents the timespan between the given timecodes</returns>
        public static TimeCode TimeSpan(TimeCode timeCode1, TimeCode timeCode2)
        {
            int msDiff;
            int sDiff;
            int mDiff;
            int hDiff;
            if (timeCode1.Value > timeCode2.Value)
            {
                msDiff = timeCode1.Milliseconds - timeCode2.Milliseconds;
                sDiff = timeCode1.Seconds - timeCode2.Seconds;
                mDiff = timeCode1.Minutes - timeCode2.Minutes;
                hDiff = timeCode1.Hours - timeCode2.Hours;
            }
            else
            {
                msDiff = timeCode2.Milliseconds - timeCode1.Milliseconds;
                sDiff = timeCode2.Seconds - timeCode1.Seconds;
                mDiff = timeCode2.Minutes - timeCode1.Minutes;
                hDiff = timeCode2.Hours - timeCode1.Hours;
            }
            if (msDiff < 0)
            {
                msDiff += 1000;
                sDiff -= 1;
            }
            if (sDiff < 0)
            {
                sDiff += 60;
                mDiff -= 1;
            }
            if (mDiff < 0)
            {
                mDiff += 60;
                hDiff -= 1;
            }
            return new TimeCode((short)hDiff, (short)mDiff, (short)sDiff, (short)msDiff);
        }
        /// <summary>
        /// Converts a timecode string represented in hh:mm:ss:msmsms to the literal value
        /// </summary>
        /// <param name="str">Timecode string represented in hh:mm:ss:msmsms</param>
        /// <returns>The string without colons and period converted to an integer</returns>
        /// <exception cref="FormatException">Timecode string was not represented in hh:mm:ss:msmsms</exception>
        public static int ToInt(string str)
        {
            Match match = TimeCodeRegex.Match(str);
            if (match.Success)
            {
                return Int32.Parse(str.Split(':', '.').Combine());
            }
            else throw new FormatException("Input string was not in a correct format.");
        }
        public static int? TryParseToInt(string str)
        {
            Match match = TimeCodeRegex.Match(str);
            if (match.Success)
            {
                return Int32.Parse(str.Split(':', '.').Combine());
            }
            else return null;
        }
        public static bool TryParseToInt(string str, out int result)
        {
            Match match = TimeCodeRegex.Match(str);
            if (match.Success)
            {
                result = Int32.Parse(str.Split(':', '.').Combine());
                return true;
            }
            else
            {
                result = 0;
                return false;
            }
        }
        public static double ToDouble(string str)
        {
            var match = TimeCodeRegex.Match(str);
            if (match.Success)
            {
                return double.Parse(str.Split(':', '.').Combine());
            }
            else throw new FormatException("Input string was not in a correct format.");
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
