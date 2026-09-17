using Xunit;

namespace SessionCapture.Maui.Tests;

/// <summary>
/// Models the cancellation-token lifecycle that SessionCaptureService uses for
/// auto-capture, so the interaction between session teardown and
/// OnShellNavigated can be tested without a device.
///
/// Mirrors SessionCaptureService:
///   StartSessionAsync / StopSessionAsync / CloseSessionSilentlyAsync
///                       -> _sessionCts cancelled + replaced
///   OnShellNavigated    -> per-navigation CTS linked to _sessionCts,
///                          await Task.Delay(500, token)
///
/// The distinction that matters: ending a session must abandon in-flight
/// captures, but navigating must not destroy the capture the previous
/// navigation is still waiting to take.
/// </summary>
internal sealed class AutoCaptureCoordinator
{
    private readonly int _delayMilliseconds;
    private CancellationTokenSource _sessionCts = new();

    public AutoCaptureCoordinator(int delayMilliseconds) => _delayMilliseconds = delayMilliseconds;

    public int CompletedCaptures { get; private set; }

    public int CancelledCaptures { get; private set; }

    /// <summary>Mirrors the session-scoped token reset in StartSessionAsync.</summary>
    public void StartSession()
    {
        _sessionCts.Cancel();
        _sessionCts.Dispose();
        _sessionCts = new CancellationTokenSource();
    }

    /// <summary>Mirrors StopSessionAsync / CloseSessionSilentlyAsync.</summary>
    public void EndSession() => _sessionCts.Cancel();

    /// <summary>
    /// Mirrors OnShellNavigated: each navigation debounces on its own token,
    /// linked to the session so teardown still cancels it.
    /// </summary>
    public async Task OnNavigatedAsync()
    {
        using var navigationCts = CancellationTokenSource.CreateLinkedTokenSource(_sessionCts.Token);
        var token = navigationCts.Token;

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
    /// The reported bug, now stated as the desired behaviour: two user-visible
    /// navigations inside the debounce window must produce two steps. Before the
    /// fix the second navigation cancelled the first's pending capture and this
    /// yielded 1 completed / 1 cancelled.
    /// </summary>
    [Fact]
    public async Task NavigationsInsideDebounceWindow_BothCapture()
    {
        var coordinator = new AutoCaptureCoordinator(Delay);

        var first = coordinator.OnNavigatedAsync();
        await Task.Delay(50);               // still inside the 500ms window
        var second = coordinator.OnNavigatedAsync();

        await Task.WhenAll(first, second);

        Assert.Equal(2, coordinator.CompletedCaptures);
        Assert.Equal(0, coordinator.CancelledCaptures);
    }

    /// <summary>
    /// A burst of rapid navigations must not lose steps. This is the tester
    /// flicking through pages while recording a bug.
    /// </summary>
    [Fact]
    public async Task RapidNavigationBurst_CapturesEveryNavigation()
    {
        var coordinator = new AutoCaptureCoordinator(Delay);

        var navigations = new List<Task>();
        for (var i = 0; i < 5; i++)
        {
            navigations.Add(coordinator.OnNavigatedAsync());
            await Task.Delay(30);
        }

        await Task.WhenAll(navigations);

        Assert.Equal(5, coordinator.CompletedCaptures);
        Assert.Equal(0, coordinator.CancelledCaptures);
    }

    /// <summary>
    /// Ending the session must still abandon captures that are mid-debounce,
    /// otherwise a step would be written into a session that has already closed.
    /// </summary>
    [Fact]
    public async Task EndingSession_CancelsPendingCapture()
    {
        var coordinator = new AutoCaptureCoordinator(Delay);

        var pending = coordinator.OnNavigatedAsync();
        await Task.Delay(50);
        coordinator.EndSession();

        await pending;

        Assert.Equal(0, coordinator.CompletedCaptures);
        Assert.Equal(1, coordinator.CancelledCaptures);
    }

    /// <summary>
    /// Starting a new session resets the token, so navigations in the new session
    /// are unaffected by the previous session's teardown.
    /// </summary>
    [Fact]
    public async Task NavigationAfterRestart_IsNotCancelledByThePreviousSession()
    {
        var coordinator = new AutoCaptureCoordinator(Delay);

        coordinator.EndSession();
        coordinator.StartSession();
        await coordinator.OnNavigatedAsync();

        Assert.Equal(1, coordinator.CompletedCaptures);
        Assert.Equal(0, coordinator.CancelledCaptures);
    }
}
