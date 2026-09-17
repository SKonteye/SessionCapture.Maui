namespace SessionCapture.Maui.Services;

internal static class ApplicationPageResolver
{
    public static Page? TryGetMainPage()
    {
        var application = Application.Current;
        if (application == null)
        {
            return null;
        }

        var window = application.Windows.FirstOrDefault();
        return window?.Page;
    }
}
