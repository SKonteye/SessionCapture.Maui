using SessionCapture.Maui.Models;
using SessionCapture.Maui.Services.Interfaces;

namespace SessionCapture.Maui.Views;

/// <summary>
/// One captured step, flattened for binding: <see cref="CapturedStep"/> stores
/// a file name, so the screenshot path has to be resolved through the service
/// before a template can show it.
/// </summary>
internal sealed class SessionStepView
{
    private SessionStepView(CapturedStep step, string? imagePath)
    {
        Step = step;
        ImagePath = imagePath;
    }

    /// <summary>The underlying step.</summary>
    public CapturedStep Step { get; }

    /// <summary>Resolved screenshot path, or <c>null</c> when there is none.</summary>
    public string? ImagePath { get; }

    /// <summary>e.g. "STEP 3 · auto".</summary>
    public string Heading => $"STEP {Step.StepNumber} · {Describe(Step.Type)}";

    /// <summary>The page the tester was on, or a placeholder.</summary>
    public string PageName =>
        string.IsNullOrWhiteSpace(Step.PageName) ? "(unknown page)" : Step.PageName;

    /// <summary>Local time the step was captured.</summary>
    public string Timestamp => Step.CapturedAt.ToLocalTime().ToString("HH:mm:ss");

    /// <summary>The note the tester left, if any.</summary>
    public string? UserNote => Step.UserNote;

    /// <summary>The binding context type recorded with the step, if any.</summary>
    public string? ViewModelName => Step.ViewModelName;

    /// <summary>True when the tester left a note on this step.</summary>
    public bool HasNote => !string.IsNullOrWhiteSpace(UserNote);

    /// <summary>
    /// True when a meaningful binding context type was recorded. Auto-captured
    /// steps store "Unknown" when the page had no view model, which is not worth
    /// showing.
    /// </summary>
    public bool HasViewModel =>
        !string.IsNullOrWhiteSpace(ViewModelName) &&
        !string.Equals(ViewModelName, "Unknown", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the screenshot is absent, so the UI can say so.</summary>
    public bool IsMissingImage => ImagePath == null;

    public static IReadOnlyList<SessionStepView> Project(
        ISessionCaptureService capture,
        CapturedSession session)
    {
        return session.Steps
            .OrderBy(step => step.StepNumber)
            .Select(step => new SessionStepView(step, capture.GetStepImagePath(session.Id, step)))
            .ToList();
    }

    private static string Describe(CaptureStepType type) => type switch
    {
        CaptureStepType.AutoNavigation => "auto",
        CaptureStepType.ManualCapture => "manual",
        CaptureStepType.Annotated => "note",
        _ => type.ToString().ToLowerInvariant()
    };
}
