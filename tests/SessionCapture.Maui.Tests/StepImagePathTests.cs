using Xunit;

namespace SessionCapture.Maui.Tests;

/// <summary>
/// Models how SessionCaptureService resolves a step's screenshot to a file on
/// disk, so GetStepImagePath can be tested without a device.
///
/// Mirrors SessionCaptureService:
///   _storageRoot  -> {AppDataDirectory}/{options.StorageFolderName}
///   session folder-> {_storageRoot}/session_{sessionId}
///   screenshot    -> {session folder}/{step.ScreenshotFileName}
///
/// The case that matters: a step can legitimately carry an empty
/// ScreenshotFileName. RecoverOrphanedSessionsAsync writes marker steps that way
/// (SessionCaptureService.cs:596). Combining that into a path yields the session
/// folder itself, which is a directory -- so a caller binding it to an Image
/// silently shows nothing instead of falling back to a placeholder. The resolver
/// must return null for those rather than a misleading path.
/// </summary>
internal static class StepImagePathResolver
{
    /// <summary>
    /// Mirrors SessionCaptureService.IsSafeSessionId. A session id becomes a
    /// folder name, so anything that can traverse out of the storage root has to
    /// be rejected before it reaches Path.Combine: "../../../etc" resolves to a
    /// path outside the app's own storage.
    /// </summary>
    public static bool IsSafeSessionId(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        foreach (var c in sessionId)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Mirrors the intended GetStepImagePath implementation.</summary>
    public static string? Resolve(string storageRoot, string sessionId, string screenshotFileName)
    {
        if (!IsSafeSessionId(sessionId) || string.IsNullOrWhiteSpace(screenshotFileName))
        {
            return null;
        }

        return Path.Combine(storageRoot, $"session_{sessionId}", screenshotFileName);
    }
}

public class StepImagePathTests
{
    private const string Root = "/data/app/SessionCapture";

    [Fact]
    public void Resolve_BuildsTheSessionFolderPath()
    {
        var path = StepImagePathResolver.Resolve(Root, "abc123", "step_001_HomePage.jpg");

        Assert.Equal(
            Path.Combine(Root, "session_abc123", "step_001_HomePage.jpg"),
            path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_ReturnsNull_ForAStepWithNoScreenshot(string fileName)
    {
        // RecoverOrphanedSessionsAsync writes marker steps with an empty file name.
        var path = StepImagePathResolver.Resolve(Root, "abc123", fileName);

        Assert.Null(path);
    }

    [Fact]
    public void Resolve_ReturnsNull_ForAnEmptySessionId()
    {
        var path = StepImagePathResolver.Resolve(Root, "", "step_001_HomePage.jpg");

        Assert.Null(path);
    }

    [Theory]
    [InlineData("../../../etc")]
    [InlineData("..")]
    [InlineData("abc/../../..")]
    [InlineData("abc/def")]
    [InlineData("abc\\def")]
    public void Resolve_ReturnsNull_ForASessionIdThatCouldTraverse(string sessionId)
    {
        var path = StepImagePathResolver.Resolve(Root, sessionId, "step_001_HomePage.jpg");

        Assert.Null(path);
    }

    [Fact]
    public void Resolve_NeverEscapesTheStorageRoot()
    {
        // Without the guard this resolves to /data/app/etc/passwd, outside Root.
        var path = StepImagePathResolver.Resolve(Root, "../../../etc", "passwd");

        Assert.Null(path);
    }

    [Fact]
    public void IsSafeSessionId_AcceptsTheIdsTheLibraryActuallyGenerates()
    {
        // CapturedSession.Id is Guid.NewGuid().ToString("N").
        Assert.True(StepImagePathResolver.IsSafeSessionId(Guid.NewGuid().ToString("N")));
    }

    [Fact]
    public void Resolve_NeverReturnsTheSessionFolderItself()
    {
        // Guards the silent-blank-image failure: Path.Combine(folder, "") == folder.
        var sessionFolder = Path.Combine(Root, "session_abc123");

        var path = StepImagePathResolver.Resolve(Root, "abc123", string.Empty);

        Assert.NotEqual(sessionFolder, path);
    }
}
