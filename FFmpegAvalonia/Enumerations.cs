using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FFmpegAvalonia
{
    public enum ItemState
    {
        Awaiting = 1,
        Progressing = 2,
        Complete = 3,
        Stopped = 4,
    }
    public enum ItemTask
    {
        Transcode = 1,
        Copy = 2,
    }
    public enum ItemLabelProgressType
    {
        None = 0,
        /// <summary>
        /// Expressed in frames or MBs depending on ItemTask
        /// </summary>
        FileDataCount = 1,
        /// <summary>
        /// Percentage of frames or MBs depending on ItemTask
        /// </summary>
        FileDataPercentage = 2,
        TotalFileCount = 3,
        TotalFilePercentage = 4,
    }
    public enum ItemProgressBarType
    {
        File = 1,
        Directory = 2,
    }
}
