using SessionCapture.Maui.Models;
using SessionCapture.Maui.Services.Interfaces;

namespace SessionCapture.Maui.Views;

/// <summary>
/// Replays one recorded session: its metadata, every step in order with the
/// screenshot that was taken, and the export actions for that session.
/// </summary>
/// <remarks>
/// Pushed by <see cref="SessionCapturePage"/>. Built in code rather than XAML so
/// the package carries no compiled-XAML dependency into a consumer's build.
/// </remarks>
public class SessionDetailPage : ContentPage
{
    private readonly ISessionCaptureService _capture;
    private readonly string _sessionId;

    private readonly Label _name = new();
    private readonly Label _meta = new();
    private readonly Label _device = new();
    private readonly Label _status = new();
    private readonly CollectionView _steps = new();
    private readonly List<Button> _actions = new();

    private bool _busy;

    /// <summary>Shows one stored session.</summary>
    /// <param name="capture">The capture service.</param>
    /// <param name="sessionId">The session to display.</param>
    public SessionDetailPage(ISessionCaptureService capture, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        _capture = capture;
        _sessionId = sessionId;

        Resources = SessionCaptureTheme.Build();
        this.ThemedPage();
        Title = "Session";

        BuildLayout();
    }

    private void BuildLayout()
    {
        _name.Style = Resources.Heading();
        _name.ThemedText();
        _meta.Style = Resources.Caption();
        _meta.ThemedMuted();
        _device.Style = Resources.Caption();
        _device.ThemedMuted();
        _status.Style = Resources.Caption();
        _status.ThemedMuted();

        _steps.ItemTemplate = new DataTemplate(BuildStepTemplate);
        _steps.EmptyView = new Label
        {
            Text = "This session recorded no steps.",
            Style = Resources.Caption()
        }.ThemedMuted();

        var exportButton = new Button { Text = "Export as ZIP" };
        exportButton.Clicked += OnExportClicked;

        var shareButton = new Button { Text = "Share" };
        shareButton.Clicked += OnShareClicked;

        var saveButton = new Button { Text = "Save to photos" };
        saveButton.Clicked += OnSaveClicked;

        var deleteButton = new Button { Text = "Delete this session" }.ThemedDanger();
        deleteButton.Clicked += OnDeleteClicked;

        // Every action works on the same session folder, and the export deletes
        // and recreates one temporary directory: two running at once would pull
        // files out from under each other.
        _actions.AddRange(new[] { exportButton, shareButton, saveButton, deleteButton });

        var header = new VerticalStackLayout
        {
            Spacing = 3,
            Children = { _name, _meta, _device }
        };

        var footer = new VerticalStackLayout
        {
            Spacing = 8,
            Children = { _status, exportButton, shareButton, saveButton, deleteButton }
        };

        var grid = new Grid
        {
            Padding = 16,
            RowSpacing = 10,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };

        grid.Add(header, 0, 0);
        grid.Add(_steps, 0, 1);
        grid.Add(footer, 0, 2);

        Content = grid;
    }

    private object BuildStepTemplate()
    {
        var thumb = new Image { Style = Resources.Thumb() }.ThemedThumb();
        thumb.SetBinding(Image.SourceProperty, static (SessionStepView s) => s.ImagePath);

        var heading = new Label { Style = Resources.StepNumber() }.ThemedPrimary();
        heading.SetBinding(Label.TextProperty, static (SessionStepView s) => s.Heading);

        var page = new Label { FontAttributes = FontAttributes.Bold }.ThemedText();
        page.SetBinding(Label.TextProperty, static (SessionStepView s) => s.PageName);

        var time = new Label { Style = Resources.Caption() }.ThemedMuted();
        time.SetBinding(Label.TextProperty, static (SessionStepView s) => s.Timestamp);

        var viewModel = new Label { Style = Resources.Caption() }.ThemedMuted();
        viewModel.SetBinding(Label.TextProperty, static (SessionStepView s) => s.ViewModelName);
        viewModel.SetBinding(IsVisibleProperty, static (SessionStepView s) => s.HasViewModel);

        var note = new Label { Style = Resources.Note() }.ThemedNote();
        note.SetBinding(Label.TextProperty, static (SessionStepView s) => s.UserNote);
        note.SetBinding(IsVisibleProperty, static (SessionStepView s) => s.HasNote);

        var missing = new Label
        {
            Text = "screenshot missing",
            Style = Resources.Caption()
        }.ThemedMuted();
        missing.SetBinding(IsVisibleProperty, static (SessionStepView s) => s.IsMissingImage);

        var text = new VerticalStackLayout
        {
            Spacing = 3,
            Children = { heading, page, time, viewModel, note, missing }
        };

        var row = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            }
        };

        row.Add(thumb, 0, 0);
        row.Add(text, 1, 0);

        return new Border
        {
            Style = Resources.Card(),
            Margin = new Thickness(0, 0, 0, 8),
            Content = row
        }.ThemedCard();
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var session = await _capture.GetSessionAsync(_sessionId);

            if (session == null)
            {
                _name.Text = "Session not found";
                _status.Text = _sessionId;
                return;
            }

            Title = session.Name;
            _name.Text = session.Name;
            _meta.Text = Describe(session);
            _device.Text = $"{session.DeviceModel} · {session.OsVersion} · app {session.AppVersion}";
            _steps.ItemsSource = SessionStepView.Project(_capture, session);
            _status.Text = $"{session.StepCount} step(s)";
        }
        catch (Exception ex)
        {
            _status.Text = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    private static string Describe(CapturedSession session)
    {
        var tester = string.IsNullOrWhiteSpace(session.TesterName)
            ? "unnamed tester"
            : session.TesterName;

        var duration = session.Duration.HasValue
            ? $"{session.Duration.Value.TotalSeconds:N0}s"
            : "still open";

        return $"{session.StartedAt.ToLocalTime():g} · {duration} · {tester}";
    }

    private async void OnExportClicked(object? sender, EventArgs e) => await RunAsync(async () =>
    {
        var path = await _capture.ExportSessionAsZipAsync(_sessionId);

        if (path == null)
        {
            return "export produced no file";
        }

        return $"{Path.GetFileName(path)} ({new FileInfo(path).Length:N0} bytes)";
    });

    private async void OnShareClicked(object? sender, EventArgs e) => await RunAsync(async () =>
    {
        await _capture.ShareSessionAsync(_sessionId);
        return "share sheet opened";
    });

    private async void OnSaveClicked(object? sender, EventArgs e) => await RunAsync(async () =>
    {
        await _capture.SaveSessionToPhotosAsync(_sessionId);
        return "saved to the photo gallery";
    });

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        // Checked before the dialog, not just inside RunAsync: otherwise a tap
        // during an in-flight export still prompts, and the tester confirms a
        // delete that is then refused.
        if (_busy)
        {
            return;
        }

        var confirmed = await DisplayAlertAsync(
            "Delete session",
            "This removes the session and its screenshots from the device.",
            "Delete",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _capture.DeleteSessionAsync(_sessionId);
            await Navigation.PopAsync();
            return "session deleted";
        });
    }

    /// <summary>
    /// Reports the outcome to the status label rather than throwing into the
    /// consumer's app: this page is a diagnostic tool and must not crash a
    /// tester's session.
    /// </summary>
    private async Task RunAsync(Func<Task<string>> action)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        SetActionsEnabled(false);

        try
        {
            _status.Text = await action();
        }
        catch (Exception ex)
        {
            _status.Text = $"{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            _busy = false;
            SetActionsEnabled(true);
        }
    }

    private void SetActionsEnabled(bool enabled)
    {
        foreach (var button in _actions)
        {
            button.IsEnabled = enabled;
        }
    }
}
