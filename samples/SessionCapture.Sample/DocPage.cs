using SessionCapture.Maui.Services.Interfaces;

namespace SessionCapture.Sample;

/// <summary>
/// Shared plumbing for the documentation pages: resolves the library service
/// the same way a consuming app would, and offers a single place to report
/// what a live demo actually did.
/// </summary>
public abstract class DocPage : ContentPage
{
    protected ISessionCaptureService? Capture { get; private set; }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Capture ??= ServiceHelper.GetService<ISessionCaptureService>();
        OnCaptureReady();
    }

    /// <summary>Called once the service has been resolved.</summary>
    protected virtual void OnCaptureReady()
    {
    }

    /// <summary>
    /// Runs a live demo, reporting the outcome (including failures) to the
    /// supplied label rather than throwing into the UI.
    /// </summary>
    protected async Task RunDemoAsync(Label target, Func<Task<string>> demo)
    {
        if (Capture == null)
        {
            target.Text = "ISessionCaptureService could not be resolved.";
            return;
        }

        try
        {
            target.Text = await demo();
        }
        catch (Exception ex)
        {
            target.Text = $"{ex.GetType().Name}: {ex.Message}";
        }
    }
}
