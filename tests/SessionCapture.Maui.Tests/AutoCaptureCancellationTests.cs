using Xunit;

namespace SessionCapture.Maui.Tests;

/// <summary>
/// Models the cancellation-token lifecycle that SessionCaptureService uses for
/// auto-capture, so the interaction between StartSessionAsync and
/// OnShellNavigated can be tested without a device.
///
/// Mirrors SessionCaptureService as of the initial commit:
///   StartSessionAsync   -> _captureCts?.Cancel()          (line 69)
///   OnShellNavigated    -> _captureCts?.Cancel();
///                          _captureCts = new(...);
///                          await Task.Delay(500, token)   (lines 497-508)
/// </summary>
internal sealed class AutoCaptureCoordinator
{
    private readonly int _delayMilliseconds;
    private CancellationTokenSource? _captureCts;

    public AutoCaptureCoordinator(int delayMilliseconds) => _delayMilliseconds = delayMilliseconds;

    public int CompletedCaptures { get; private set; }

    public int CancelledCaptures { get; private set; }

    /// <summary>Mirrors StartSessionAsync's cancellation of any pending capture.</summary>
    public void StartSession() => _captureCts?.Cancel();

    /// <summary>Mirrors OnShellNavigated: cancel the previous capture, then debounce.</summary>
    public async Task OnNavigatedAsync()
    {
        _captureCts?.Cancel();
        _captureCts = new CancellationTokenSource();
        var token = _captureCts.Token;

        try
        {
            await Task.Delay(_delayMilliseconds, token);
            token.ThrowIfCancellationRequested();
            CompletedCaptures++;
        }
        catch (OperationCanceledException)
        {
            CancelledCaptures++;
        }
    }
}

public class AutoCaptureCancellationTests
{
    private const int Delay = 500;

    [Fact]
    public async Task SingleNavigation_CapturesOnce()
    {
        var coordinator = new AutoCaptureCoordinator(Delay);

        await coordinator.OnNavigatedAsync();

        Assert.Equal(1, coordinator.CompletedCaptures);
        Assert.Equal(0, coordinator.CancelledCaptures);
    }

    [Fact]
    public async Task StartSession_DoesNotCancelALaterNavigation()
    {
        var coordinator = new AutoCaptureCoordinator(Delay);

        coordinator.StartSession();
        await coordinator.OnNavigatedAsync();

        Assert.Equal(1, coordinator.CompletedCaptures);
    }

    /// <summary>
    /// The reported bug: navigating again while a capture is still in its debounce
    /// window cancels the pending capture. Two user-visible navigations therefore
    /// produce a single step, not two.
    /// </summary>
    [Fact]
    public async Task NavigationsInsideDebounceWindow_CancelThePendingCapture()
    {
        var coordinator = new AutoCaptureCoordinator(Delay);

        var first = coordinator.OnNavigatedAsync();
        await Task.Delay(50);               // still inside the 500ms window
        var second = coordinator.OnNavigatedAsync();

        await Task.WhenAll(first, second);

        Assert.Equal(1, coordinator.CancelledCaptures);
        Assert.Equal(1, coordinator.CompletedCaptures);
    }

    /// <summary>
    /// Navigating away before the debounce elapses loses the capture entirely:
    /// the pending one is cancelled and nothing replaces it if the session stops.
    /// </summary>
    [Fact]
    public async Task NavigationCancelledBeforeDelayElapses_CapturesNothing()
    {
        var coordinator = new AutoCaptureCoordinator(Delay);

        var pending = coordinator.OnNavigatedAsync();
        await Task.Delay(50);
        coordinator.StartSession();         // cancels the pending capture

        await pending;

        Assert.Equal(0, coordinator.CompletedCaptures);
        Assert.Equal(1, coordinator.CancelledCaptures);
    }
}
