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

    Task DeleteSessionAsync(string sessionId);

    Task DeleteAllSessionsAsync();

    Task SaveSessionToPhotosAsync(string sessionId);

    Task<string?> ExportSessionAsZipAsync(string sessionId);

    Task ShareSessionAsync(string sessionId);

    event EventHandler<CapturedStep>? StepCaptured;

    event EventHandler<CapturedSession>? SessionStarted;

    event EventHandler<CapturedSession>? SessionStopped;
}
