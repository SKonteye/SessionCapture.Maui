namespace SessionCapture.Sample;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Pushed from Stored sessions rather than shown in the flyout: it needs
        // a session id, so it is meaningless as a top-level destination.
        Routing.RegisterRoute(nameof(SessionDetailPage), typeof(SessionDetailPage));
    }
}
