using System.ComponentModel;
using SessionCapture.Maui.Options;
using SessionCapture.Maui.Services.Interfaces;

namespace SessionCapture.Maui.Services;

internal sealed class SessionCaptureLifecycleManager : IDisposable
{
    private readonly ISessionCaptureService _sessionCaptureService;
    private readonly SessionCaptureOptions _options;
    private Application? _application;
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

        _sessionCaptureService.DetachFromShell();
        _trackedShell = null;
    }

    private void OnApplicationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Application.MainPage))
        {
            RefreshShellAttachment();
        }
    }

    private void RefreshShellAttachment()
    {
        if (!_options.Enabled)
        {
            _sessionCaptureService.DetachFromShell();
            _trackedShell = null;
            return;
        }

        var shell = FindShell(ApplicationPageResolver.TryGetMainPage());
        if (ReferenceEquals(shell, _trackedShell))
        {
            return;
        }

        _sessionCaptureService.DetachFromShell();
        _trackedShell = shell;

        if (shell != null)
        {
            _sessionCaptureService.AttachToShell();
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
