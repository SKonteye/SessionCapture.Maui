namespace SessionCapture.Sample;

internal static class ServiceHelper
{
    public static T? GetService<T>() where T : class
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        return services?.GetService(typeof(T)) as T;
    }
}
