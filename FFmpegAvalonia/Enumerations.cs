namespace FFmpegAvalonia;
public enum ItemState
{
    Awaiting = 0,
    Progressing = 1,
    Complete = 2,
    Stopped = 3,
}
public enum ItemTask
{
    Transcode = 0,
    Copy = 1,
    Trim = 2,
    UploadAWS = 3,
    Checksum = 4,
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
    None = 0,
    File = 1,
    Directory = 2,
}
