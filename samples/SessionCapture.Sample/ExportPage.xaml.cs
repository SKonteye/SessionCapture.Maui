namespace SessionCapture.Sample;

public partial class ExportPage : DocPage
{
    public ExportPage()
    {
        InitializeComponent();
    }

    private async Task<string> WithNewestSessionAsync(Func<string, Task<string>> action)
    {
        var sessions = await Capture!.GetAllSessionsAsync();
        var newest = sessions.OrderByDescending(s => s.StartedAt).FirstOrDefault();

        return newest == null
            ? "no stored sessions yet - record one first"
            : await action(newest.Id);
    }

    private async void OnExportClicked(object? sender, EventArgs e)
        => await RunDemoAsync(ResultLabel, () => WithNewestSessionAsync(async id =>
        {
            var path = await Capture!.ExportSessionAsZipAsync(id);
            if (path == null)
            {
                return "export returned null";
            }

            var size = new FileInfo(path).Length;
            return $"zip written\n{Path.GetFileName(path)}\n{size:N0} bytes";
        }));

    private async void OnShareClicked(object? sender, EventArgs e)
        => await RunDemoAsync(ResultLabel, () => WithNewestSessionAsync(async id =>
        {
            await Capture!.ShareSessionAsync(id);
            return "share sheet opened";
        }));

    private async void OnSaveClicked(object? sender, EventArgs e)
        => await RunDemoAsync(ResultLabel, () => WithNewestSessionAsync(async id =>
        {
            await Capture!.SaveSessionToPhotosAsync(id);
            return "saved to the photo gallery";
        }));
}
