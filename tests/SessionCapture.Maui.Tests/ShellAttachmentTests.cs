using Xunit;

namespace SessionCapture.Maui.Tests;

/// <summary>
/// Models AttachToShell / DetachFromShell subscription bookkeeping.
///
/// Mirrors SessionCaptureService:
///   AttachToShell   -> UnhookShell(); hook Navigated when a Shell is present;
///                      show the overlay for any root page
///   UnhookShell     -> unsubscribe and hide the overlay, session untouched
///   DetachFromShell -> UnhookShell() and close any active session
/// </summary>
internal sealed class FakeShell
{
    public int SubscriberCount { get; private set; }

    public void Subscribe() => SubscriberCount++;

    public void Unsubscribe() => SubscriberCount--;
}

internal sealed class ShellAttacher
{
    private FakeShell? _attached;

    public int SilentSessionCloses { get; private set; }

    public bool IsSessionActive { get; set; }

    public bool OverlayVisible { get; private set; }

    public void Attach(FakeShell? current)
    {
        // Re-attaching must not end a recording in progress.
        Unhook();

        if (current != null)
        {
            _attached = current;
            _attached.Subscribe();
        }

        // The overlay does not need a Shell.
        OverlayVisible = true;
    }

    private void Unhook()
    {
        if (_attached != null)
        {
            _attached.Unsubscribe();
            _attached = null;
        }

        OverlayVisible = false;
    }

    public void Detach()
    {
        Unhook();

        // DetachFromShell closes any in-flight session silently.
        if (IsSessionActive)
        {
            SilentSessionCloses++;
            IsSessionActive = false;
        }
    }
}

public class ShellAttachmentTests
{
    [Fact]
    public void Attach_SubscribesExactlyOnce()
    {
        var shell = new FakeShell();
        var attacher = new ShellAttacher();

        attacher.Attach(shell);

        Assert.Equal(1, shell.SubscriberCount);
    }

    [Fact]
    public void AttachTwice_DoesNotDoubleSubscribe()
    {
        var shell = new FakeShell();
        var attacher = new ShellAttacher();

        attacher.Attach(shell);
        attacher.Attach(shell);

        Assert.Equal(1, shell.SubscriberCount);
    }

    /// <summary>
    /// Non-Shell apps still get the overlay, so a tester can capture manually.
    /// Only navigation hooking needs a Shell. Verified on the iOS simulator:
    /// a plain ContentPage root recorded a manual capture successfully.
    /// </summary>
    [Fact]
    public void AttachWithNoShell_ShowsOverlayWithoutSubscribing()
    {
        var attacher = new ShellAttacher();

        attacher.Attach(null);

        Assert.True(attacher.OverlayVisible);
    }

    /// <summary>
    /// Re-attaching after a root-page swap must keep an in-progress recording.
    /// Previously this silently ended the session, so a login-to-shell
    /// transition mid-recording discarded the tester's work.
    /// </summary>
    [Fact]
    public void ReAttachDuringActiveSession_KeepsTheSessionRunning()
    {
        var shell = new FakeShell();
        var attacher = new ShellAttacher { IsSessionActive = true };

        attacher.Attach(shell);

        Assert.Equal(0, attacher.SilentSessionCloses);
        Assert.True(attacher.IsSessionActive);
    }

    /// <summary>
    /// An explicit detach (teardown, or the library being disabled) still ends
    /// the session, so nothing is written into a session nobody is watching.
    /// </summary>
    [Fact]
    public void ExplicitDetachDuringActiveSession_ClosesTheSession()
    {
        var attacher = new ShellAttacher { IsSessionActive = true };
        attacher.Attach(new FakeShell());

        attacher.Detach();

        Assert.Equal(1, attacher.SilentSessionCloses);
        Assert.False(attacher.IsSessionActive);
    }
}
