using System.Collections.Generic;

namespace FFmpegAvalonia;

internal static class HashMaps
{
    public static readonly Dictionary<string, string> FileFormats = new()
    {
        { ".3gp", "audio" },
        { ".aa", "audio" },
        { ".aac", "audio" },
        { ".aax", "audio" },
        { ".act", "audio" },
        { ".aiff", "audio" },
        { ".alac", "audio" },
        { ".amr", "audio" },
        { ".ape", "audio" },
        { ".au", "audio" },
        { ".awb", "audio" },
        { ".dss", "audio" },
        { ".dvf", "audio" },
        { ".flac", "audio" },
        { ".gsm", "audio" },
        { ".iklax", "audio" },
        { ".ivs", "audio" },
        { ".m4a", "audio" },
        { ".m4b", "audio" },
        { ".m4p", "audio" },
        { ".mmf", "audio" },
        { ".movpkg", "audio" },
        { ".mp3", "audio" },
        { ".mpc", "audio" },
        { ".msv", "audio" },
        { ".nmf", "audio" },
        { ".ogg", "audio" },
        { ".oga", "audio" },
        { ".mogg", "audio" },
        { ".opus", "audio" },
        { ".ra", "audio" },
        { ".rm", "audio" },
        { ".raw", "audio" },
        { ".rf64", "audio" },
        { ".sln", "audio" },
        { ".tta", "audio" },
        { ".voc", "audio" },
        { ".vox", "audio" },
        { ".wav", "audio" },
        { ".wma", "audio" },
        { ".wv", "audio" },
        { ".webm", "audio" },
        { ".8svx", "audio" },
        { ".cda", "audio" },
    };
}
