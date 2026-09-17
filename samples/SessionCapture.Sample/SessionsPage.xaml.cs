using SessionCapture.Maui.Models;
using SessionCapture.Maui.Services.Interfaces;

namespace SessionCapture.Sample;

public partial class SessionsPage : ContentPage
{
    private ISessionCaptureService? _capture;
    private CapturedSession? _newest;

    public SessionsPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _capture = ServiceHelper.GetService<ISessionCaptureService>();
        await ReloadAsync();
    }

    private async void OnReloadClicked(object? sender, EventArgs e) => await ReloadAsync();

    private async Task ReloadAsync()
    {
        if (_capture == null)
        {
            ResultLabel.Text = "service: NOT RESOLVED";
            return;
        }

        try
        {
            var sessions = await _capture.GetAllSessionsAsync();
            SessionList.ItemsSource = sessions;
            _newest = sessions.OrderByDescending(s => s.StartedAt).FirstOrDefault();
            ResultLabel.Text = $"loaded {sessions.Count} session(s)";
        }
        catch (Exception ex)
        {
            ResultLabel.Text = $"EX: {ex.GetType().Name}: {ex.Message}";
        }
    }

    private async void OnExportClicked(object? sender, EventArgs e)
        => await Run(async () =>
        {
            var path = await _capture!.ExportSessionAsZipAsync(_newest!.Id);
            return path == null ? "export returned null" : $"zip: {path}";
        });

    private async void OnShareClicked(object? sender, EventArgs e)
        => await Run(async () =>
        {
            await _capture!.ShareSessionAsync(_newest!.Id);
            return "share sheet invoked";
        });

    private async void OnSaveClicked(object? sender, EventArgs e)
        => await Run(async () =>
        {
            await _capture!.SaveSessionToPhotosAsync(_newest!.Id);
            return "saved to photos";
        });

    private async void OnDeleteAllClicked(object? sender, EventArgs e)
        => await Run(async () =>
        {
            await _capture!.DeleteAllSessionsAsync();
            await ReloadAsync();
            return "deleted all";
        });

    private async Task Run(Func<Task<string>> action)
    {
        if (_capture == null)
        {
            ResultLabel.Text = "service: NOT RESOLVED";
            return;
        }

        if (_newest == null)
        {
            ResultLabel.Text = "no session available; record one first";
            return;
        }

        try
        {
            ResultLabel.Text = await action();
        }
        catch (Exception ex)
        {
            ResultLabel.Text = $"EX: {ex.GetType().Name}: {ex.Message}";
        }
    }
}
