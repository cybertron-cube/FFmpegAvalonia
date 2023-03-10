using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFmpegAvalonia.AppSettingsX
{
    internal class Settings : PropertyReflection
    {
        public string FFmpegPath { get; set; } = "";
        public string FrameCountMethod { get; set; } = "GetFrameCountApproximate";
        public bool AutoOverwriteCheck { get; set; }
        public bool CopySourceCheck { get; set; }
    }
}
