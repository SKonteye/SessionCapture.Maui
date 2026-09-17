using SessionCapture.Maui.Models;

namespace SessionCapture.Sample;

public partial class CapturingPage : DocPage
{
    public CapturingPage()
    {
        InitializeComponent();
    }

    private async void OnCaptureClicked(object? sender, EventArgs e)
        => await RunDemoAsync(ResultLabel, async () =>
        {
            if (!Capture!.IsSessionActive)
            {
                return "no active session - start one on page 2 first";
            }

            var before = Capture.CurrentSession?.StepCount ?? 0;
            await Capture.CaptureScreenshotAsync(
                nameof(CapturingPage), nameof(CapturingPage), CaptureStepType.ManualCapture);
            var after = Capture.CurrentSession?.StepCount ?? 0;

            return after > before
                ? $"captured step {after}"
                : "capture did not add a step";
        });

    private async void OnNoteClicked(object? sender, EventArgs e)
        => await RunDemoAsync(ResultLabel, async () =>
        {
            if (!Capture!.IsSessionActive)
            {
                return "no active session - start one on page 2 first";
            }

            var before = Capture.CurrentSession?.StepCount ?? 0;
            await Capture.CaptureWithNoteAsync(
                nameof(CapturingPage), nameof(CapturingPage), "Captured from the docs app");
            var after = Capture.CurrentSession?.StepCount ?? 0;

            return after > before
                ? $"captured step {after} with a note"
                : "capture did not add a step";
        });
}
