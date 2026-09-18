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
/// ScreenshotFileName. CloseSessionSilentlyAsync writes recovery steps that way
/// (SessionCaptureService.cs:596). Combining that into a path yields the session
/// folder itself, which is a directory -- so a caller binding it to an Image
/// silently shows nothing instead of falling back to a placeholder. The resolver
/// must return null for those rather than a misleading path.
/// </summary>
internal static class StepImagePathResolver
{
    /// <summary>Mirrors the intended GetStepImagePath implementation.</summary>
    public static string? Resolve(string storageRoot, string sessionId, string screenshotFileName)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(screenshotFileName))
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
        // CloseSessionSilentlyAsync writes recovery steps with an empty file name.
        var path = StepImagePathResolver.Resolve(Root, "abc123", fileName);

        Assert.Null(path);
    }

    [Fact]
    public void Resolve_ReturnsNull_ForAnEmptySessionId()
    {
        var path = StepImagePathResolver.Resolve(Root, "", "step_001_HomePage.jpg");

        Assert.Null(path);
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
