using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Maui.LifecycleEvents;
using SessionCapture.Maui.Options;
using SessionCapture.Maui.Services;
using SessionCapture.Maui.Services.Interfaces;

namespace SessionCapture.Maui.Extensions;

public static class MauiAppBuilderExtensions
{
    public static MauiAppBuilder UseSessionCapture(
        this MauiAppBuilder builder,
        IConfiguration? configuration = null,
        Action<SessionCaptureOptions>? configure = null)
    {
        var optionsBuilder = builder.Services.AddOptions<SessionCaptureOptions>();
        if (configuration != null)
        {
            optionsBuilder.Bind(configuration);
        }

        if (configure != null)
        {
            builder.Services.PostConfigure(configure);
        }

        builder.Services.TryAddSingleton<SessionCaptureService>();
        builder.Services.TryAddSingleton<ISessionCaptureService>(serviceProvider =>
            serviceProvider.GetRequiredService<SessionCaptureService>());
        builder.Services.TryAddSingleton<SessionCaptureLifecycleManager>();

#if ANDROID
        builder.Services.TryAddSingleton<ISessionCaptureOverlayService, Platforms.Android.Services.SessionCaptureOverlayService>();
        builder.Services.TryAddSingleton<IMediaSaveService, Platforms.Android.Services.MediaSaveService>();
        builder.ConfigureLifecycleEvents(events =>
        {
            events.AddAndroid(android => android.OnCreate((activity, bundle) =>
            {
                SessionCaptureStartup.ScheduleInitialization();
            }));
        });
#elif IOS
        builder.Services.TryAddSingleton<ISessionCaptureOverlayService, Platforms.iOS.Services.SessionCaptureOverlayService>();
        builder.Services.TryAddSingleton<IMediaSaveService, Platforms.iOS.Services.MediaSaveService>();
        builder.ConfigureLifecycleEvents(events =>
        {
            events.AddiOS(ios => ios.FinishedLaunching((application, launchOptions) =>
            {
                SessionCaptureStartup.ScheduleInitialization();
                return true;
            }));
        });
#endif

        return builder;
    }
}
