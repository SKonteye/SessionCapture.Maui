namespace SessionCapture.Sample;

public partial class ReviewPage : DocPage
{
    public ReviewPage()
    {
        InitializeComponent();
    }

    private async void OnOpenNewestClicked(object? sender, EventArgs e)
        => await RunDemoAsync(ResultLabel, async () =>
        {
            var sessions = await Capture!.GetAllSessionsAsync();
            var newest = sessions.OrderByDescending(s => s.StartedAt).FirstOrDefault();

            if (newest == null)
            {
                return "no stored sessions yet - record one first";
            }

            await Shell.Current.GoToAsync(
                $"{nameof(SessionDetailPage)}?id={Uri.EscapeDataString(newest.Id)}");

            return $"opened {newest.Name}";
        });
}
