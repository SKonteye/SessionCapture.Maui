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
    private readonly ISessionCaptureService? _capture;

    private readonly Label _status = new();
    private readonly CollectionView _sessions = new();

    /// <summary>
    /// Creates the page, resolving <see cref="ISessionCaptureService"/> from the
    /// app's service provider. Use this when the library was registered with
    /// <c>UseSessionCapture</c>, which is the usual case.
    /// </summary>
    public SessionCapturePage()
        : this(ApplicationServiceResolver.TryGetServices()?.GetService(typeof(ISessionCaptureService)) as ISessionCaptureService)
    {
    }

    /// <summary>
    /// Creates the page with an explicit service, for apps that resolve their
    /// own dependencies.
    /// </summary>
    /// <param name="capture">The capture service, or <c>null</c> to show a diagnostic.</param>
    public SessionCapturePage(ISessionCaptureService? capture)
    {
        _capture = capture;

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
            Style = (Style)Resources["SessionCaptureHeading"]
        }.ThemedText();

        var subtitle = new Label
        {
            Text = "Tap a session to review its steps and export it.",
            Style = (Style)Resources["SessionCaptureCaption"]
        }.ThemedMuted();

        _status.Style = (Style)Resources["SessionCaptureCaption"];
        _status.ThemedMuted();

        _sessions.SelectionMode = SelectionMode.Single;
        _sessions.SelectionChanged += OnSessionSelected;
        _sessions.ItemTemplate = new DataTemplate(BuildSessionTemplate);
        _sessions.EmptyView = new Label
        {
            Text = "No sessions recorded yet.",
            Style = (Style)Resources["SessionCaptureCaption"]
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

        var steps = new Label { Style = (Style)Resources["SessionCaptureCaption"] }.ThemedMuted();
        steps.SetBinding(
            Label.TextProperty,
            static (CapturedSession s) => s.StepCount,
            stringFormat: "{0} step(s)");

        var started = new Label { Style = (Style)Resources["SessionCaptureCaption"] }.ThemedMuted();
        started.SetBinding(
            Label.TextProperty,
            static (CapturedSession s) => s.StartedAt,
            stringFormat: "{0:g}");

        var tester = new Label { Style = (Style)Resources["SessionCaptureCaption"] }.ThemedMuted();
        tester.SetBinding(
            Label.TextProperty,
            static (CapturedSession s) => s.TesterName,
            stringFormat: "tester: {0}");

        return new Border
        {
            Style = (Style)Resources["SessionCaptureCard"],
            Margin = new Thickness(0, 0, 0, 8),
            Content = new VerticalStackLayout
            {
                Spacing = 3,
                Children = { name, steps, started, tester }
            }
        };
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();
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

        if (Navigation == null)
        {
            _status.Text = "No navigation host: push SessionCapturePage inside a NavigationPage or Shell.";
            return;
        }

        await Navigation.PushAsync(new SessionDetailPage(_capture, session.Id));
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
