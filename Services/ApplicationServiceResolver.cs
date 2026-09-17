namespace SessionCapture.Maui.Services;

internal static class ApplicationServiceResolver
{
    public static IServiceProvider? TryGetServices()
    {
        return Application.Current?.Handler?.MauiContext?.Services;
    }
}
