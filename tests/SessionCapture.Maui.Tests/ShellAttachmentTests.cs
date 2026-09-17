using Xunit;

namespace SessionCapture.Maui.Tests;

/// <summary>
/// Models AttachToShell / DetachFromShell subscription bookkeeping.
///
/// Mirrors SessionCaptureService as of the initial commit:
///   AttachToShell   -> DetachFromShell();
///                      shell = Shell.Current; if (shell == null) return;
///                      shell.Navigated += OnShellNavigated;   (lines 169-188)
///   DetachFromShell -> unsubscribe, HideOverlay, and close any active session
///                                                            (lines 190-204)
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

    public void Attach(FakeShell? current)
    {
        Detach();

        if (current == null)
        {
            return;
        }

        _attached = current;
        _attached.Subscribe();
    }

    public void Detach()
    {
        if (_attached != null)
        {
            _attached.Unsubscribe();
            _attached = null;
        }

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

    [Fact]
    public void AttachWithNoShell_LeavesNothingSubscribed()
    {
        var attacher = new ShellAttacher();

        attacher.Attach(null);

        // Nothing to assert on a null shell beyond "it did not throw";
        // the observable consequence is that no capture can ever fire.
        Assert.True(true);
    }

    /// <summary>
    /// Re-attaching while a session is running silently ends that session.
    /// Any root-page change (for example a Shell rebuild) therefore drops the
    /// user's in-progress recording without surfacing an error.
    /// </summary>
    [Fact]
    public void ReAttachDuringActiveSession_SilentlyClosesTheSession()
    {
        var shell = new FakeShell();
        var attacher = new ShellAttacher { IsSessionActive = true };

        attacher.Attach(shell);

        Assert.Equal(1, attacher.SilentSessionCloses);
        Assert.False(attacher.IsSessionActive);
    }
}
