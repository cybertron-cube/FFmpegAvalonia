using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFmpegAvalonia.AppSettingsX
{
    internal class Profile : PropertyReflection //maybe add position element
    {
        public string Name { get; set; } = "";
        public string Arguments { get; set; } = "";
        public string OutputExtension { get; set; } = "";
    }
}
