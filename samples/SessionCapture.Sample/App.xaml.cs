namespace SessionCapture.Sample;

public partial class App : Application
{
    public const string RootModeKey = "Sample.RootMode";

    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // The sample deliberately supports two root layouts so the library can be
        // verified against both. Shell is the happy path; NavigationPage is the
        // case a real consumer app may well use.
        var useShell = Preferences.Get(RootModeKey, true);

        Page root = useShell
            ? new AppShell()
            : new NavigationPage(new MainPage());

        return new Window(root);
    }

    public static void SwitchRootMode(bool useShell)
    {
        Preferences.Set(RootModeKey, useShell);

        if (Current?.Windows.FirstOrDefault() is { } window)
        {
            window.Page = useShell
                ? new AppShell()
                : new NavigationPage(new MainPage());
        }
    }
}
