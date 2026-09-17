namespace SessionCapture.Sample;

public partial class StoredSessionsPage : DocPage
{
    public StoredSessionsPage()
    {
        InitializeComponent();
    }

    protected override async void OnCaptureReady() => await ReloadAsync();

    private async void OnReloadClicked(object? sender, EventArgs e) => await ReloadAsync();

    private async void OnDeleteAllClicked(object? sender, EventArgs e)
    {
        await RunDemoAsync(ResultLabel, async () =>
        {
            await Capture!.DeleteAllSessionsAsync();
            return "all sessions deleted";
        });

        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        if (Capture == null)
        {
            ResultLabel.Text = "service: NOT RESOLVED";
            return;
        }

        try
        {
            var sessions = await Capture.GetAllSessionsAsync();
            var ordered = sessions.OrderByDescending(s => s.StartedAt).ToList();
            SessionList.ItemsSource = ordered;
            ResultLabel.Text = $"{ordered.Count} session(s) on disk";
        }
        catch (Exception ex)
        {
            ResultLabel.Text = $"{ex.GetType().Name}: {ex.Message}";
        }
    }
}
