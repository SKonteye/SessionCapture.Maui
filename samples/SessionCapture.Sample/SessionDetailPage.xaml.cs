using SessionCapture.Maui.Models;

namespace SessionCapture.Sample;

/// <summary>
/// One step, flattened for binding: CapturedStep carries only a file name, so
/// the screenshot path has to be resolved through the service before the
/// template can show it.
/// </summary>
public sealed class StepView
{
    public required string Heading { get; init; }

    public required string PageName { get; init; }

    public required string Timestamp { get; init; }

    public string? ViewModelName { get; init; }

    public string? UserNote { get; init; }

    public string? ImagePath { get; init; }

    public bool HasViewModel => !string.IsNullOrWhiteSpace(ViewModelName);

    public bool HasNote => !string.IsNullOrWhiteSpace(UserNote);

    public bool IsMissingImage => ImagePath == null;
}

/// <summary>
/// Replays one recorded session: its metadata, every step in order with the
/// screenshot that was taken, and the per-session export actions.
/// </summary>
[QueryProperty(nameof(SessionId), "id")]
public partial class SessionDetailPage : DocPage
{
    private string? _sessionId;

    public SessionDetailPage()
    {
        InitializeComponent();
    }

    public string? SessionId
    {
        get => _sessionId;
        set
        {
            // Shell URL-encodes query values, so an id round-trips through Uri.
            _sessionId = value == null ? null : Uri.UnescapeDataString(value);
        }
    }

    protected override async void OnCaptureReady() => await LoadAsync();

    private async Task LoadAsync()
    {
        if (Capture == null || string.IsNullOrEmpty(_sessionId))
        {
            ResultLabel.Text = "no session id supplied";
            return;
        }

        try
        {
            var session = await Capture.GetSessionAsync(_sessionId);

            if (session == null)
            {
                SessionName.Text = "Session not found";
                ResultLabel.Text = $"GetSessionAsync returned null for {_sessionId}";
                return;
            }

            Title = session.Name;
            SessionName.Text = session.Name;
            SessionMeta.Text = DescribeRun(session);
            SessionDevice.Text = $"{session.DeviceModel} · {session.OsVersion} · app {session.AppVersion}";
            SessionIdLabel.Text = session.Id;

            StepList.ItemsSource = session.Steps
                .OrderBy(step => step.StepNumber)
                .Select(step => new StepView
                {
                    Heading = $"STEP {step.StepNumber} · {Describe(step.Type)}",
                    PageName = string.IsNullOrWhiteSpace(step.PageName) ? "(unknown page)" : step.PageName,
                    Timestamp = step.CapturedAt.ToString("HH:mm:ss"),
                    ViewModelName = step.ViewModelName,
                    UserNote = step.UserNote,
                    ImagePath = Capture.GetStepImagePath(session.Id, step)
                })
                .ToList();

            ResultLabel.Text = $"{session.StepCount} step(s)";
        }
        catch (Exception ex)
        {
            ResultLabel.Text = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    private static string DescribeRun(CapturedSession session)
    {
        var tester = string.IsNullOrWhiteSpace(session.TesterName) ? "unnamed tester" : session.TesterName;
        var duration = session.Duration.HasValue
            ? $"{session.Duration.Value.TotalSeconds:N0}s"
            : "still open";

        return $"{session.StartedAt:g} · {duration} · {tester}";
    }

    private static string Describe(CaptureStepType type) => type switch
    {
        CaptureStepType.AutoNavigation => "auto",
        CaptureStepType.ManualCapture => "manual",
        CaptureStepType.Annotated => "note",
        _ => type.ToString()
    };

    private async void OnExportClicked(object? sender, EventArgs e)
        => await RunDemoAsync(ResultLabel, async () =>
        {
            var path = await Capture!.ExportSessionAsZipAsync(_sessionId!);
            if (path == null)
            {
                return "export returned null";
            }

            var size = new FileInfo(path).Length;
            return $"zip written\n{Path.GetFileName(path)}\n{size:N0} bytes";
        });

    private async void OnShareClicked(object? sender, EventArgs e)
        => await RunDemoAsync(ResultLabel, async () =>
        {
            await Capture!.ShareSessionAsync(_sessionId!);
            return "share sheet opened";
        });

    private async void OnSaveClicked(object? sender, EventArgs e)
        => await RunDemoAsync(ResultLabel, async () =>
        {
            await Capture!.SaveSessionToPhotosAsync(_sessionId!);
            return "saved to the photo gallery";
        });

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        var confirmed = await DisplayAlertAsync(
            "Delete session",
            "This removes the session and its screenshots from the device.",
            "Delete",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        await RunDemoAsync(ResultLabel, async () =>
        {
            await Capture!.DeleteSessionAsync(_sessionId!);
            await Shell.Current.GoToAsync("..");
            return "session deleted";
        });
    }
}
