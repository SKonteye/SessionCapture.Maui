namespace SessionCapture.Maui.Services.Interfaces;

public interface ISessionCaptureOverlayService
{
    void ShowOverlay(ISessionCaptureService sessionCaptureService);

    void HideOverlay();

    void UpdateState(bool isRecording, int captureCount);

    void SuspendOverlay();

    void ResumeOverlay();
}
