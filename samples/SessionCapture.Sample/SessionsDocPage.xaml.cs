namespace SessionCapture.Sample;

public partial class SessionsDocPage : DocPage
{
    public SessionsDocPage()
    {
        InitializeComponent();
    }

    protected override void OnCaptureReady() => Refresh();

    private async void OnStartClicked(object? sender, EventArgs e)
    {
        await RunDemoAsync(ResultLabel, async () =>
        {
            var session = await Capture!.StartSessionAsync("Docs walkthrough", "QA Tester");
            return $"started: {session.Name}\nid: {session.Id[..8]}...";
        });

        Refresh();
    }

    private async void OnStopClicked(object? sender, EventArgs e)
    {
        await RunDemoAsync(ResultLabel, async () =>
        {
            var session = await Capture!.StopSessionAsync();
            return $"stopped: {session.Name}\nsteps: {session.StepCount}\nduration: {session.Duration}";
        });

        Refresh();
    }

    private void Refresh()
    {
        if (Capture == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(ResultLabel.Text))
        {
            ResultLabel.Text = $"IsSessionActive: {Capture.IsSessionActive}";
        }
    }
}
