using System.Text;
using SessionCapture.Maui.Models;
using SessionCapture.Maui.Services.Interfaces;

namespace SessionCapture.Sample;

public partial class MainPage : ContentPage
{
    private readonly StringBuilder _log = new();
    private ISessionCaptureService? _capture;

    public MainPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _capture = ServiceHelper.GetService<ISessionCaptureService>();

        // Temporary diagnostic: does the library's Navigated subscription fire?
        if (Shell.Current is { } shell)
        {
            shell.Navigated -= OnProbeNavigated;
            shell.Navigated += OnProbeNavigated;
            Append($"probe attached to Shell #{shell.GetHashCode()}");
        }

        if (_capture == null)
        {
            Append("FAIL: ISessionCaptureService not resolvable.");
            RefreshState();
            return;
        }

        _capture.SessionStarted += OnSessionStarted;
        _capture.SessionStopped += OnSessionStopped;
        _capture.StepCaptured += OnStepCaptured;

        RefreshState();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (_capture != null)
        {
            _capture.SessionStarted -= OnSessionStarted;
            _capture.SessionStopped -= OnSessionStopped;
            _capture.StepCaptured -= OnStepCaptured;
        }
    }

    private void OnProbeNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        // Written to disk so the count of Navigated events per user-visible
        // navigation can be read back without driving the simulator UI.
        try
        {
            File.AppendAllText(
                Path.Combine(FileSystem.AppDataDirectory, "probe.log"),
                $"{DateTime.UtcNow:HH:mm:ss.fff} Navigated loc={e.Current?.Location} src={e.Source}\n");
        }
        catch
        {
            // diagnostic only
        }

        MainThread.BeginInvokeOnMainThread(() =>
            Append($"PROBE Navigated: {e.Current?.Location} (src {e.Source})"));
    }

    private void OnSessionStarted(object? sender, CapturedSession e)
        => MainThread.BeginInvokeOnMainThread(() =>
        {
            Append($"SessionStarted: {e.Name}");
            RefreshState();
        });

    private void OnSessionStopped(object? sender, CapturedSession e)
        => MainThread.BeginInvokeOnMainThread(() =>
        {
            Append($"SessionStopped: {e.StepCount} steps");
            RefreshState();
        });

    private void OnStepCaptured(object? sender, CapturedStep e)
        => MainThread.BeginInvokeOnMainThread(() =>
        {
            Append($"Step {e.StepNumber}: {e.PageName} ({e.Type})");
            RefreshState();
        });

    private async void OnStartClicked(object? sender, EventArgs e)
        => await Guard(async () => await _capture!.StartSessionAsync("POC run", "Tester"));

    private async void OnStopClicked(object? sender, EventArgs e)
        => await Guard(async () => await _capture!.StopSessionAsync());

    private async void OnCaptureClicked(object? sender, EventArgs e)
        => await Guard(async () => await _capture!.CaptureScreenshotAsync(
            nameof(MainPage), nameof(MainPage), CaptureStepType.ManualCapture));

    private async void OnCaptureNoteClicked(object? sender, EventArgs e)
        => await Guard(async () => await _capture!.CaptureWithNoteAsync(
            nameof(MainPage), nameof(MainPage), "note from POC"));

    private async void OnGoFormClicked(object? sender, EventArgs e)
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("//form");
        }
        else
        {
            await Navigation.PushAsync(new FormPage());
        }
    }

    private async void OnGoDetailClicked(object? sender, EventArgs e)
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("detail");
        }
        else
        {
            await Navigation.PushAsync(new DetailPage());
        }
    }

    private void OnSwitchRootClicked(object? sender, EventArgs e)
    {
        var useShell = Preferences.Get(App.RootModeKey, true);
        App.SwitchRootMode(!useShell);
    }

    private async Task Guard(Func<Task> action)
    {
        if (_capture == null)
        {
            Append("FAIL: service is null.");
            return;
        }

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Append($"EX: {ex.GetType().Name}: {ex.Message}");
        }

        RefreshState();
    }

    private void Append(string line)
    {
        _log.Insert(0, $"{DateTime.Now:HH:mm:ss}  {line}\n");
        LogLabel.Text = _log.ToString();
    }

    private void RefreshState()
    {
        var useShell = Preferences.Get(App.RootModeKey, true);
        RootModeLabel.Text = useShell
            ? "Root: Shell (library supported path)"
            : "Root: NavigationPage (no Shell)";
        SwitchRootButton.Text = useShell
            ? "Switch to NavigationPage root"
            : "Switch to Shell root";

        if (_capture == null)
        {
            StateLabel.Text = "service: NOT RESOLVED";
            return;
        }

        StateLabel.Text =
            $"IsEnabled:       {_capture.IsEnabled}\n" +
            $"IsSessionActive: {_capture.IsSessionActive}\n" +
            $"Steps:           {_capture.CurrentSession?.StepCount ?? 0}\n" +
            $"Shell.Current:   {(Shell.Current == null ? "null" : "present")}";
    }
}
