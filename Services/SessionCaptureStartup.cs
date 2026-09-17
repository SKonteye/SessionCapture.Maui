using Microsoft.Extensions.DependencyInjection;

namespace SessionCapture.Maui.Services;

internal static class SessionCaptureStartup
{
    private static int _scheduled;

    public static void ScheduleInitialization()
    {
        if (Interlocked.Exchange(ref _scheduled, 1) == 1)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(() => _ = TryInitializeAsync());
    }

    private static async Task TryInitializeAsync()
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var services = ApplicationServiceResolver.TryGetServices();
            var lifecycleManager = services?.GetService<SessionCaptureLifecycleManager>();
            if (lifecycleManager != null)
            {
                lifecycleManager.Start();
                return;
            }

            await Task.Delay(250);
        }
    }
}
