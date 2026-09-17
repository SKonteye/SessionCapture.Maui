namespace SessionCapture.Maui.Options;

public sealed class SessionCaptureOptions
{
    public bool Enabled { get; set; }

    public bool AutoCaptureOnNavigation { get; set; } = true;

    public int CaptureQuality { get; set; } = 80;

    public int MaxSessionsRetained { get; set; } = 20;

    public int MaxScreenshotsPerSession { get; set; } = 200;

    public string StorageFolderName { get; set; } = "SessionCapture";

    public string GalleryAlbumName { get; set; } = "SessionCapture";
}
