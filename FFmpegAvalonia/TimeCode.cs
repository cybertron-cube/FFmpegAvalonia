using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExtensionMethods;

namespace FFmpegAvalonia
{
    public class TimeCode
    {
        private int _timeCode;
        private int _hours;
        private int _minutes;
        private int _seconds;
        private int _milliseconds;
        private string _timeCodeString;
        public TimeCode FromString(string text)
        {
            //ex "00:00:00.000"
            throw new NotImplementedException();
        }
        public static string TimeSpan(TimeCode timeCodeOne, TimeCode timeCodeTwo)
        {
            throw new NotImplementedException();
        }
        public static int ToInt(string str)
        {
            return int.Parse(str.Split(':', '.').Combine());
        }
    }
}
