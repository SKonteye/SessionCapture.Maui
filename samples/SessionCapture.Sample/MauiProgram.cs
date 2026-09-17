using Microsoft.Extensions.Logging;
using SessionCapture.Maui.Extensions;

namespace SessionCapture.Sample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // No appsettings.json here on purpose: the sample exercises the
        // code-configured path, which is the one most consumers reach for first.
        builder.UseSessionCapture(configure: options =>
        {
            options.Enabled = true;
            options.AutoCaptureOnNavigation = true;
            options.CaptureQuality = 80;
            options.MaxSessionsRetained = 20;
            options.MaxScreenshotsPerSession = 200;
        });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
