namespace FFmpegAvalonia.AppSettingsX;

    internal class Settings : PropertyReflection
    {
        public string FFmpegPath { get; set; } = "";
        public string FrameCountMethod { get; set; } = "GetFrameCountApproximate";
        public string UpdateTarget { get; set; } = "release";
        public bool AutoOverwriteCheck { get; set; }
}
