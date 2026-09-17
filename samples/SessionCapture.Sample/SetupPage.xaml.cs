using SessionCapture.Maui.Services.Interfaces;

namespace SessionCapture.Sample;

public partial class SetupPage : DocPage
{
    public SetupPage()
    {
        InitializeComponent();
    }

    private async void OnCheckClicked(object? sender, EventArgs e)
        => await RunDemoAsync(ResultLabel, () =>
        {
            var overlay = ServiceHelper.GetService<ISessionCaptureOverlayService>();
            var media = ServiceHelper.GetService<IMediaSaveService>();

            return Task.FromResult(
                $"ISessionCaptureService:        {(Capture != null ? "resolved" : "MISSING")}\n" +
                $"ISessionCaptureOverlayService: {(overlay != null ? "resolved" : "MISSING")}\n" +
                $"IMediaSaveService:             {(media != null ? "resolved" : "MISSING")}\n" +
                $"Enabled:                       {Capture?.IsEnabled}");
        });
}
