namespace SessionCapture.Sample;

public partial class OverviewPage : DocPage
{
    public OverviewPage()
    {
        InitializeComponent();
    }

    protected override void OnCaptureReady()
    {
        StateLabel.Text = Capture == null
            ? "service: NOT RESOLVED"
            : $"IsEnabled:       {Capture.IsEnabled}\n" +
              $"IsSessionActive: {Capture.IsSessionActive}\n" +
              $"Steps:           {Capture.CurrentSession?.StepCount ?? 0}";
    }
}
