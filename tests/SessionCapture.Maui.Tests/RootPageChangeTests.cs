using Xunit;

namespace SessionCapture.Maui.Tests;

/// <summary>
/// Models how SessionCaptureLifecycleManager notices that the window's root page
/// changed, so re-attachment can be tested without a device.
///
/// Verified on the iOS 26.5 simulator before this test was written: booting with
/// a plain ContentPage root and then assigning Windows[0].Page = new AppShell()
/// left the library detached. Shell.Current was non-null afterwards, yet a
/// navigation recorded no step, because assigning Windows[0].Page raises no
/// PropertyChanged for the obsolete Application.MainPage the manager listened on.
/// </summary>
internal sealed class FakeWindow
{
    private object? _page;

    /// <summary>Raised for the window's own Page property, as MAUI does.</summary>
    public event Action<string>? PropertyChanged;

    public object? Page
    {
        get => _page;
        set
        {
            if (ReferenceEquals(_page, value))
            {
                return;
            }

            _page = value;
            PropertyChanged?.Invoke(nameof(Page));
        }
    }
}

/// <summary>
/// Mirrors the lifecycle manager: resolves the root page from the window and
/// attaches only when it finds a Shell.
/// </summary>
internal sealed class LifecycleAttacher
{
    private readonly FakeWindow _window;
    private object? _trackedShell;

    public LifecycleAttacher(FakeWindow window)
    {
        _window = window;
        _window.PropertyChanged += OnWindowPropertyChanged;
    }

    public int Attachments { get; private set; }

    public int Detachments { get; private set; }

    public bool IsAttached => _trackedShell != null;

    public void Start() => Refresh();

    private void OnWindowPropertyChanged(string propertyName)
    {
        if (propertyName == nameof(FakeWindow.Page))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        // Only a Shell root is attachable, matching FindShell.
        var shell = _window.Page as FakeShell;

        if (ReferenceEquals(shell, _trackedShell))
        {
            return;
        }

        if (_trackedShell != null)
        {
            Detachments++;
        }

        _trackedShell = shell;

        if (shell != null)
        {
            Attachments++;
        }
    }
}

public class RootPageChangeTests
{
    private sealed class PlainPage
    {
    }

    /// <summary>
    /// The reported bug as desired behaviour: an app that boots on a non-Shell
    /// root (a login or splash page) and later swaps in a Shell must attach at
    /// that point. Before the fix nothing re-attached and capture stayed dead
    /// for the rest of the process.
    /// </summary>
    [Fact]
    public void SwappingRootPageToAShell_Attaches()
    {
        var window = new FakeWindow { Page = new PlainPage() };
        var attacher = new LifecycleAttacher(window);

        attacher.Start();
        Assert.False(attacher.IsAttached);   // no Shell yet

        window.Page = new FakeShell();

        Assert.True(attacher.IsAttached);
        Assert.Equal(1, attacher.Attachments);
    }

    /// <summary>
    /// Replacing one Shell with another (a rebuild after logout, say) must move
    /// the subscription to the new Shell rather than leave it on the dead one.
    /// </summary>
    [Fact]
    public void ReplacingOneShellWithAnother_ReattachesToTheNewShell()
    {
        var window = new FakeWindow { Page = new FakeShell() };
        var attacher = new LifecycleAttacher(window);

        attacher.Start();
        Assert.Equal(1, attacher.Attachments);

        window.Page = new FakeShell();

        Assert.Equal(2, attacher.Attachments);
        Assert.Equal(1, attacher.Detachments);
    }

    /// <summary>
    /// Swapping a Shell out for a plain page must detach, so no handler is left
    /// on a Shell that is no longer displayed.
    /// </summary>
    [Fact]
    public void SwappingAwayFromAShell_Detaches()
    {
        var window = new FakeWindow { Page = new FakeShell() };
        var attacher = new LifecycleAttacher(window);

        attacher.Start();
        window.Page = new PlainPage();

        Assert.False(attacher.IsAttached);
        Assert.Equal(1, attacher.Detachments);
    }

    /// <summary>
    /// Assigning the same page again must not churn the subscription.
    /// </summary>
    [Fact]
    public void ReassigningTheSameShell_DoesNotReattach()
    {
        var shell = new FakeShell();
        var window = new FakeWindow { Page = shell };
        var attacher = new LifecycleAttacher(window);

        attacher.Start();
        window.Page = shell;

        Assert.Equal(1, attacher.Attachments);
        Assert.Equal(0, attacher.Detachments);
    }
}
