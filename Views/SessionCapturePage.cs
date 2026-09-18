using SessionCapture.Maui.Models;
using SessionCapture.Maui.Services;
using SessionCapture.Maui.Services.Interfaces;

namespace SessionCapture.Maui.Views;

/// <summary>
/// The built-in session browser: every recorded session, newest first, with a
/// tap-through to the steps, screenshots and export actions for each one.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole review UI. A consuming app needs no screens of its own:
/// </para>
/// <code>
/// await Navigation.PushAsync(new SessionCapturePage());
/// </code>
/// <para>
/// It needs a navigation host to push the detail page onto, so present it
/// inside a <see cref="NavigationPage"/>, a Shell, or push it from a page that
/// already has one.
/// </para>
/// </remarks>
public class SessionCapturePage : ContentPage
{
    private readonly bool _resolveFromContainer;

    private ISessionCaptureService? _capture;

    private readonly Label _status = new();
    private readonly CollectionView _sessions = new();

    /// <summary>
    /// Creates the page, resolving <see cref="ISessionCaptureService"/> from the
    /// app's service provider. Use this when the library was registered with
    /// <c>UseSessionCapture</c>, which is the usual case.
    /// </summary>
    public SessionCapturePage()
    {
        // Resolved on appearing, not here: constructing the page during startup,
        // as in new NavigationPage(new SessionCapturePage()), runs before
        // Application.Current.Handler exists, and capturing null now would leave
        // the page claiming UseSessionCapture was never called.
        _resolveFromContainer = true;

        Resources = SessionCaptureTheme.Build();
        this.ThemedPage();
        Title = "Sessions";

        BuildLayout();
    }

    /// <summary>
    /// Creates the page with an explicit service, for apps that resolve their
    /// own dependencies.
    /// </summary>
    /// <param name="capture">The capture service, or <c>null</c> to show a diagnostic.</param>
    public SessionCapturePage(ISessionCaptureService? capture)
    {
        _capture = capture;
        _resolveFromContainer = false;

        Resources = SessionCaptureTheme.Build();
        this.ThemedPage();
        Title = "Sessions";

        BuildLayout();
    }

    private void BuildLayout()
    {
        var heading = new Label
        {
            Text = "Recorded sessions",
            Style = Resources.Heading()
        }.ThemedText();

        var subtitle = new Label
        {
            Text = "Tap a session to review its steps and export it.",
            Style = Resources.Caption()
        }.ThemedMuted();

        _status.Style = Resources.Caption();
        _status.ThemedMuted();

        _sessions.SelectionMode = SelectionMode.Single;
        _sessions.SelectionChanged += OnSessionSelected;
        _sessions.ItemTemplate = new DataTemplate(BuildSessionTemplate);
        _sessions.EmptyView = new Label
        {
            Text = "No sessions recorded yet.",
            Style = Resources.Caption()
        }.ThemedMuted();

        var reload = new Button { Text = "Reload" };
        reload.Clicked += async (_, _) => await LoadAsync();

        var deleteAll = new Button { Text = "Delete all" }.ThemedDanger();
        deleteAll.Clicked += OnDeleteAllClicked;

        var grid = new Grid
        {
            Padding = 16,
            RowSpacing = 10,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };

        grid.Add(heading, 0, 0);
        grid.Add(subtitle, 0, 1);
        grid.Add(_sessions, 0, 2);
        grid.Add(
            new VerticalStackLayout
            {
                Spacing = 8,
                Children = { _status, reload, deleteAll }
            },
            0,
            3);

        Content = grid;
    }

    private object BuildSessionTemplate()
    {
        var name = new Label { FontAttributes = FontAttributes.Bold }.ThemedText();
        name.SetBinding(Label.TextProperty, static (CapturedSession s) => s.Name);

        var steps = new Label { Style = Resources.Caption() }.ThemedMuted();
        steps.SetBinding(
            Label.TextProperty,
            static (CapturedSession s) => s.StepCount,
            stringFormat: "{0} step(s)");

        var started = new Label { Style = Resources.Caption() }.ThemedMuted();
        started.SetBinding(
            Label.TextProperty,
            static (CapturedSession s) => s.StartedAt,
            stringFormat: "{0:g}");

        var tester = new Label { Style = Resources.Caption() }.ThemedMuted();
        tester.SetBinding(
            Label.TextProperty,
            static (CapturedSession s) => s.TesterName,
            stringFormat: "tester: {0}");

        return new Border
        {
            Style = Resources.Card(),
            Margin = new Thickness(0, 0, 0, 8),
            Content = new VerticalStackLayout
            {
                Spacing = 3,
                Children = { name, steps, started, tester }
            }
        }.ThemedCard();
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // The container is only reliably reachable once the page is attached.
        if (_resolveFromContainer && _capture == null)
        {
            _capture = ApplicationServiceResolver.TryGetServices()
                ?.GetService(typeof(ISessionCaptureService)) as ISessionCaptureService;
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_capture == null)
        {
            _status.Text = "ISessionCaptureService is not registered. Call UseSessionCapture in MauiProgram.";
            return;
        }

        try
        {
            var sessions = await _capture.GetAllSessionsAsync();
            var ordered = sessions.OrderByDescending(session => session.StartedAt).ToList();

            _sessions.ItemsSource = ordered;
            _status.Text = $"{ordered.Count} session(s) on this device";
        }
        catch (Exception ex)
        {
            _status.Text = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    private async void OnSessionSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not CapturedSession session || _capture == null)
        {
            return;
        }

        // Clear it, or returning leaves the row selected and re-tapping it
        // raises nothing.
        _sessions.SelectedItem = null;

        // Navigation is never null -- MAUI hands back a proxy even with no host
        // behind it -- so check for a real host instead, and still contain a
        // failed push rather than letting it escape this async void handler into
        // the consumer's app.
        if (Shell.Current == null && Application.Current?.Windows
                .Any(window => window.Page is NavigationPage) != true)
        {
            _status.Text = "No navigation host: present SessionCapturePage inside a NavigationPage or Shell.";
            return;
        }

        try
        {
            await Navigation.PushAsync(new SessionDetailPage(_capture, session.Id));
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not open the session: {ex.Message}";
        }
    }

    private async void OnDeleteAllClicked(object? sender, EventArgs e)
    {
        if (_capture == null)
        {
            return;
        }

        var confirmed = await DisplayAlertAsync(
            "Delete all sessions",
            "This removes every recorded session and its screenshots from the device.",
            "Delete all",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        try
        {
            await _capture.DeleteAllSessionsAsync();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _status.Text = $"{ex.GetType().Name}: {ex.Message}";
        }
    }
}
