using SessionCapture.Maui.Models;

namespace SessionCapture.Maui.Services.Interfaces;

public interface ISessionCaptureService
{
    bool IsEnabled { get; }

    bool IsSessionActive { get; }

    CapturedSession? CurrentSession { get; }

    Task<CapturedSession> StartSessionAsync(string? sessionName = null, string? testerName = null);

    Task<CapturedSession> StopSessionAsync();

    Task CloseSessionSilentlyAsync();

    Task CaptureScreenshotAsync(
        string pageName,
        string viewModelName,
        CaptureStepType type = CaptureStepType.AutoNavigation);

    Task CaptureWithNoteAsync(string pageName, string viewModelName, string note);

    void AttachToShell();

    void DetachFromShell();

    Task<IReadOnlyList<CapturedSession>> GetAllSessionsAsync();

    Task<CapturedSession?> GetSessionAsync(string sessionId);

    /// <summary>
    /// Resolves the on-disk path of a step's screenshot, for displaying a
    /// recorded session back to the tester.
    /// </summary>
    /// <param name="sessionId">The session the step belongs to.</param>
    /// <param name="step">A step from that session.</param>
    /// <returns>
    /// The full path to the screenshot, or <c>null</c> when the step carries no
    /// screenshot or the file is no longer on disk. Steps recorded by
    /// <see cref="CloseSessionSilentlyAsync"/> have no screenshot.
    /// </returns>
    string? GetStepImagePath(string sessionId, CapturedStep step);

    Task DeleteSessionAsync(string sessionId);

    Task DeleteAllSessionsAsync();

    Task SaveSessionToPhotosAsync(string sessionId);

    Task<string?> ExportSessionAsZipAsync(string sessionId);

    Task ShareSessionAsync(string sessionId);

    event EventHandler<CapturedStep>? StepCaptured;

    event EventHandler<CapturedSession>? SessionStarted;

    event EventHandler<CapturedSession>? SessionStopped;
}
