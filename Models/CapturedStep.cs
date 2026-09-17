namespace SessionCapture.Maui.Models;

public sealed class CapturedStep
{
    public int StepNumber { get; set; }

    public DateTime CapturedAt { get; set; }

    public string PageName { get; set; } = string.Empty;

    public string ViewModelName { get; set; } = string.Empty;

    public string ScreenshotFileName { get; set; } = string.Empty;

    public string? UserNote { get; set; }

    public CaptureStepType Type { get; set; } = CaptureStepType.AutoNavigation;
}
