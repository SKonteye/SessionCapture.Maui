using System.ComponentModel;
using SessionCapture.Maui.Options;
using SessionCapture.Maui.Services.Interfaces;

namespace SessionCapture.Maui.Services;

internal sealed class SessionCaptureLifecycleManager : IDisposable
{
    private readonly ISessionCaptureService _sessionCaptureService;
    private readonly SessionCaptureOptions _options;
    private Application? _application;
    private Window? _trackedWindow;
    private Page? _trackedPage;
    private Shell? _trackedShell;
    private bool _started;

    public SessionCaptureLifecycleManager(
        ISessionCaptureService sessionCaptureService,
        Microsoft.Extensions.Options.IOptions<SessionCaptureOptions> options)
    {
        _sessionCaptureService = sessionCaptureService;
        _options = options.Value;
    }

    public void Start()
    {
        if (_started)
        {
            RefreshShellAttachment();
            return;
        }

        _application = Application.Current;
        if (_application == null)
        {
            return;
        }

        _application.PropertyChanged += OnApplicationPropertyChanged;
        _started = true;
        RefreshShellAttachment();
    }

    public void Dispose()
    {
        if (_application != null)
        {
            _application.PropertyChanged -= OnApplicationPropertyChanged;
        }

        TrackWindow(null);
        _sessionCaptureService.DetachFromShell();
        _trackedPage = null;
        _trackedShell = null;
    }

    private void OnApplicationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Windows changing means a new window may need its Page tracked; the
        // obsolete MainPage is still honoured for apps that set it.
        if (e.PropertyName == nameof(Application.Windows) ||
            e.PropertyName == "MainPage")
        {
            RefreshShellAttachment();
        }
    }

    private void OnWindowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Assigning Windows[0].Page raises this rather than any MainPage
        // notification, so this is what actually fires when an app swaps its
        // root page (login -> shell, onboarding -> app).
        if (e.PropertyName == nameof(Window.Page))
        {
            RefreshShellAttachment();
        }
    }

    /// <summary>
    /// Keeps the PropertyChanged subscription pointed at the current window, so
    /// a later root-page swap is noticed.
    /// </summary>
    private void TrackWindow(Window? window)
    {
        if (ReferenceEquals(window, _trackedWindow))
        {
            return;
        }

        if (_trackedWindow != null)
        {
            _trackedWindow.PropertyChanged -= OnWindowPropertyChanged;
        }

        _trackedWindow = window;

        if (_trackedWindow != null)
        {
            _trackedWindow.PropertyChanged += OnWindowPropertyChanged;
        }
    }

    private void RefreshShellAttachment()
    {
        if (!_options.Enabled)
        {
            _sessionCaptureService.DetachFromShell();
            _trackedPage = null;
            _trackedShell = null;
            return;
        }

        var window = Application.Current?.Windows.FirstOrDefault();
        TrackWindow(window);

        var page = window?.Page;
        var shell = FindShell(page);

        // Compare on the root page, not the Shell: two different non-Shell roots
        // both resolve to a null Shell, and comparing those would skip the
        // re-attach the second page needs for its overlay.
        if (ReferenceEquals(page, _trackedPage) && ReferenceEquals(shell, _trackedShell))
        {
            return;
        }

        _trackedPage = page;
        _trackedShell = shell;

        // Attach for any root page: the overlay works without a Shell, and
        // AttachToShell hooks navigation only when one is present. AttachToShell
        // unhooks the previous root itself, without ending an active session, so
        // a swap mid-recording keeps recording.
        if (page != null)
        {
            _sessionCaptureService.AttachToShell();
        }
        else
        {
            _sessionCaptureService.DetachFromShell();
        }
    }

    private static Shell? FindShell(Page? page)
    {
        return page switch
        {
            null => null,
            Shell shell => shell,
            NavigationPage navigationPage => FindShell(navigationPage.CurrentPage),
            TabbedPage tabbedPage => FindShell(tabbedPage.CurrentPage),
            FlyoutPage flyoutPage => FindShell(flyoutPage.Detail),
            _ => null
        };
    }
}
