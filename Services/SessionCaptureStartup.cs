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

            // Android raises OnCreate before MAUI creates the Window, and
            // BeginInvokeOnMainThread runs inline on the main thread, so the
            // first attempt sees no window. Application does not notify when one
            // is added, so starting now would never attach the overlay. Once a
            // window exists, the lifecycle manager follows its Page itself.
            var hasWindow = Application.Current?.Windows.Count > 0;
            if (lifecycleManager != null && hasWindow)
            {
                lifecycleManager.Start();
                return;
            }

            await Task.Delay(250);
        }
    }
}
