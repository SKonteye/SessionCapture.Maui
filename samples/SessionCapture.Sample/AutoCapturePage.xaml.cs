using SessionCapture.Maui.Models;

namespace SessionCapture.Sample;

public partial class AutoCapturePage : DocPage
{
    public AutoCapturePage()
    {
        InitializeComponent();
    }

    protected override void OnCaptureReady() => Report();

    private void OnRefreshClicked(object? sender, EventArgs e) => Report();

    private void Report()
    {
        if (Capture == null)
        {
            ResultLabel.Text = "service: NOT RESOLVED";
            return;
        }

        var session = Capture.CurrentSession;
        if (session == null)
        {
            ResultLabel.Text = "no active session - start one on page 2";
            return;
        }

        var auto = session.Steps.Count(s => s.Type == CaptureStepType.AutoNavigation);
        var manual = session.Steps.Count(s => s.Type != CaptureStepType.AutoNavigation);

        ResultLabel.Text =
            $"session: {session.Name}\n" +
            $"auto steps:   {auto}\n" +
            $"manual steps: {manual}\n" +
            $"total:        {session.StepCount}";
    }
}
