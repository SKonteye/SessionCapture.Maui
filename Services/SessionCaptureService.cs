using System.IO.Compression;
using System.Text;
using System.Text.Json;
using SessionCapture.Maui.Models;
using SessionCapture.Maui.Options;
using SessionCapture.Maui.Services.Interfaces;
using SkiaSharp;

namespace SessionCapture.Maui.Services;

public sealed class SessionCaptureService : ISessionCaptureService
{
    private const int AutoCaptureDelayMilliseconds = 500;
    private const int OverlaySettleDelayMilliseconds = 50;

    private readonly ISessionCaptureOverlayService _overlayService;
    private readonly IMediaSaveService _mediaSaveService;
    private readonly SessionCaptureOptions _options;
    private readonly string _storageRoot;
    private readonly string _indexFilePath;
    private readonly SemaphoreSlim _storageLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;

    private CapturedSession? _currentSession;
    private string? _currentSessionFolder;
    private Shell? _attachedShell;

    /// <summary>
    /// Scoped to the session, not to a single navigation. Ending a session
    /// cancels it so in-flight captures are abandoned; each navigation links its
    /// own token to this one rather than replacing it, so that navigating does
    /// not destroy the capture a previous navigation is still waiting to take.
    /// </summary>
    private CancellationTokenSource _sessionCts = new();
    private bool _autoCaptureMaxReached;

    public SessionCaptureService(
        ISessionCaptureOverlayService overlayService,
        IMediaSaveService mediaSaveService,
        Microsoft.Extensions.Options.IOptions<SessionCaptureOptions> options)
    {
        _overlayService = overlayService;
        _mediaSaveService = mediaSaveService;
        _options = options.Value;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        _storageRoot = Path.Combine(FileSystem.AppDataDirectory, _options.StorageFolderName);
        _indexFilePath = Path.Combine(_storageRoot, "index.json");

        Directory.CreateDirectory(_storageRoot);
        _ = RecoverOrphanedSessionsAsync();
    }

    public bool IsEnabled => _options.Enabled;

    public bool IsSessionActive => _currentSession is { IsActive: true };

    public CapturedSession? CurrentSession => _currentSession;

    public event EventHandler<CapturedStep>? StepCaptured;

    public event EventHandler<CapturedSession>? SessionStarted;

    public event EventHandler<CapturedSession>? SessionStopped;

    public async Task<CapturedSession> StartSessionAsync(string? sessionName = null, string? testerName = null)
    {
        EnsureEnabled();

        ResetSessionToken();
        _autoCaptureMaxReached = false;

        var session = new CapturedSession
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = string.IsNullOrWhiteSpace(sessionName)
                ? $"Session {DateTime.Now:yyyy-MM-dd HH:mm}"
                : sessionName.Trim(),
            StartedAt = DateTime.UtcNow,
            DeviceModel = DeviceInfo.Current.Model,
            OsVersion = DeviceInfo.Current.VersionString,
            AppVersion = AppInfo.Current.VersionString,
            TesterName = string.IsNullOrWhiteSpace(testerName) ? null : testerName.Trim()
        };

        await _storageLock.WaitAsync();
        try
        {
            if (_currentSession != null)
            {
                throw new InvalidOperationException("A capture session is already active.");
            }

            _currentSessionFolder = Path.Combine(_storageRoot, $"session_{session.Id}");
            Directory.CreateDirectory(_currentSessionFolder);
            _currentSession = session;

            await SaveSessionJsonUnsafeAsync();
            await UpdateIndexUnsafeAsync(session);
            await PurgeOldSessionsUnsafeAsync();
        }
        finally
        {
            _storageLock.Release();
        }

        SessionStarted?.Invoke(this, session);
        _overlayService.UpdateState(true, 0);
        return session;
    }

    public async Task<CapturedSession> StopSessionAsync()
    {
        EnsureEnabled();

        CapturedSession completedSession;

        await _storageLock.WaitAsync();
        try
        {
            if (_currentSession == null)
            {
                throw new InvalidOperationException("No active capture session to stop.");
            }

            _sessionCts.Cancel();
            _currentSession.EndedAt = DateTime.UtcNow;
            await SaveSessionJsonUnsafeAsync();
            await UpdateIndexUnsafeAsync(_currentSession);

            completedSession = _currentSession;
            _currentSession = null;
            _currentSessionFolder = null;
            _autoCaptureMaxReached = false;
        }
        finally
        {
            _storageLock.Release();
        }

        SessionStopped?.Invoke(this, completedSession);
        _overlayService.UpdateState(false, 0);
        return completedSession;
    }

    public async Task CloseSessionSilentlyAsync()
    {
        await _storageLock.WaitAsync();
        try
        {
            if (_currentSession == null)
            {
                return;
            }

            _sessionCts.Cancel();
            _currentSession.EndedAt = DateTime.UtcNow;
            await SaveSessionJsonUnsafeAsync();
            await UpdateIndexUnsafeAsync(_currentSession);
            _currentSession = null;
            _currentSessionFolder = null;
            _autoCaptureMaxReached = false;
        }
        finally
        {
            _storageLock.Release();
        }
    }

    public void AttachToShell()
    {
        if (!IsEnabled)
        {
            return;
        }

        // Unhook without ending the session: re-attaching after a root-page swap
        // must not discard a recording that is in progress.
        UnhookShell();

        // A Shell is needed to hook navigation, but not to show the overlay or
        // capture manually. Apps with a plain page root still get a working
        // capture button; only auto-capture requires Shell.
        var shell = Shell.Current;
        if (shell != null)
        {
            _attachedShell = shell;
            _attachedShell.Navigated += OnShellNavigated;
        }

        _overlayService.ShowOverlay(this);
        _overlayService.UpdateState(IsSessionActive, _currentSession?.StepCount ?? 0);
    }

    public void DetachFromShell()
    {
        UnhookShell();

        if (IsSessionActive)
        {
            _ = CloseSessionSilentlyAsync();
        }
    }

    /// <summary>
    /// Removes the navigation hook and hides the overlay, leaving any active
    /// session running. Used when re-attaching to a new root page, where ending
    /// the recording would lose the tester's work.
    /// </summary>
    private void UnhookShell()
    {
        if (_attachedShell != null)
        {
            _attachedShell.Navigated -= OnShellNavigated;
            _attachedShell = null;
        }

        _overlayService.HideOverlay();
    }

    public async Task CaptureScreenshotAsync(
        string pageName,
        string viewModelName,
        CaptureStepType type = CaptureStepType.AutoNavigation)
    {
        EnsureEnabled();

        if (!IsSessionActive)
        {
            return;
        }

        var capturedAt = DateTime.UtcNow;

        try
        {
            var screenshotResult = await CaptureWithoutOverlayAsync("Manual capture");
            if (screenshotResult == null)
            {
                return;
            }

            await SaveStepAsync(
                pageName,
                viewModelName,
                capturedAt,
                screenshotResult,
                type,
                userNote: null,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SessionCaptureService: CaptureScreenshotAsync failed: {ex.Message}");
        }
    }

    public async Task CaptureWithNoteAsync(string pageName, string viewModelName, string note)
    {
        EnsureEnabled();

        if (!IsSessionActive)
        {
            return;
        }

        var capturedAt = DateTime.UtcNow;

        try
        {
            var screenshotResult = await CaptureWithoutOverlayAsync("Annotated capture");
            if (screenshotResult == null)
            {
                return;
            }

            await SaveStepAsync(
                pageName,
                viewModelName,
                capturedAt,
                screenshotResult,
                CaptureStepType.Annotated,
                string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SessionCaptureService: CaptureWithNoteAsync failed: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<CapturedSession>> GetAllSessionsAsync()
    {
        await _storageLock.WaitAsync();
        try
        {
            var sessions = await ReadIndexUnsafeAsync();
            return sessions.OrderByDescending(session => session.StartedAt).ToList();
        }
        finally
        {
            _storageLock.Release();
        }
    }

    public async Task<CapturedSession?> GetSessionAsync(string sessionId)
    {
        var sessionFolder = TryGetSessionFolder(sessionId);

        if (sessionFolder == null)
        {
            return null;
        }

        var sessionFile = Path.Combine(sessionFolder, "session.json");

        if (!File.Exists(sessionFile))
        {
            return null;
        }

        await _storageLock.WaitAsync();
        try
        {
            var json = await File.ReadAllTextAsync(sessionFile);
            return JsonSerializer.Deserialize<CapturedSession>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SessionCaptureService: Failed to read session {sessionId}: {ex.Message}");
            return null;
        }
        finally
        {
            _storageLock.Release();
        }
    }

    /// <summary>
    /// A session id becomes a folder name, so it has to be checked before it
    /// reaches <see cref="Path.Combine(string, string)"/>. The ids this library
    /// generates are <c>Guid.NewGuid().ToString("N")</c>, but these methods are
    /// public API and a caller can pass anything: "../../../etc" would resolve
    /// to a path outside the storage root.
    /// </summary>
    private static bool IsSafeSessionId(string sessionId)
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

    /// <summary>
    /// The folder holding one session, or <c>null</c> when the id could escape
    /// the storage root.
    /// </summary>
    private string? TryGetSessionFolder(string sessionId)
        => IsSafeSessionId(sessionId)
            ? Path.Combine(_storageRoot, $"session_{sessionId}")
            : null;

    public string? GetStepImagePath(string sessionId, CapturedStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        var sessionFolder = TryGetSessionFolder(sessionId);

        // A step can legitimately have no screenshot: CloseSessionSilentlyAsync
        // records recovery steps that way. Combining an empty name would yield
        // the session folder, and binding a directory to an Image shows nothing
        // rather than failing, so return null and let the caller show a
        // placeholder.
        if (sessionFolder == null || string.IsNullOrWhiteSpace(step.ScreenshotFileName))
        {
            return null;
        }

        // The file name comes from our own index, but it is combined into a
        // path just like the id, so it gets the same treatment.
        if (Path.GetFileName(step.ScreenshotFileName) != step.ScreenshotFileName)
        {
            return null;
        }

        var path = Path.Combine(sessionFolder, step.ScreenshotFileName);

        return File.Exists(path) ? path : null;
    }

    public async Task DeleteSessionAsync(string sessionId)
    {
        await _storageLock.WaitAsync();
        try
        {
            if (_currentSession?.Id == sessionId)
            {
                throw new InvalidOperationException("Cannot delete the session that is currently being recorded.");
            }

            // Guarded before the recursive delete: an id that traverses out of
            // the storage root would take the delete with it.
            var sessionFolder = TryGetSessionFolder(sessionId);
            if (sessionFolder != null && Directory.Exists(sessionFolder))
            {
                Directory.Delete(sessionFolder, recursive: true);
            }

            var sessions = await ReadIndexUnsafeAsync();
            sessions.RemoveAll(session => session.Id == sessionId);
            await WriteIndexUnsafeAsync(sessions);
        }
        finally
        {
            _storageLock.Release();
        }
    }

    public async Task DeleteAllSessionsAsync()
    {
        await _storageLock.WaitAsync();
        try
        {
            if (_currentSession != null)
            {
                throw new InvalidOperationException("Stop the active capture session before deleting all sessions.");
            }

            if (Directory.Exists(_storageRoot))
            {
                foreach (var directory in Directory.GetDirectories(_storageRoot, "session_*"))
                {
                    try
                    {
                        Directory.Delete(directory, recursive: true);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"SessionCaptureService: Failed to delete {directory}: {ex.Message}");
                    }
                }
            }

            await WriteIndexUnsafeAsync(new List<CapturedSession>());
        }
        finally
        {
            _storageLock.Release();
        }
    }

    public async Task SaveSessionToPhotosAsync(string sessionId)
    {
        EnsureEnabled();

        var session = await GetSessionAsync(sessionId);
        if (session == null)
        {
            return;
        }

        // GetSessionAsync already rejected an unsafe id, but resolve through the
        // same helper rather than rebuilding the path and relying on that.
        var sessionFolder = TryGetSessionFolder(sessionId);
        if (sessionFolder == null)
        {
            return;
        }

        foreach (var step in session.Steps)
        {
            var filePath = Path.Combine(sessionFolder, step.ScreenshotFileName);
            if (!File.Exists(filePath))
            {
                continue;
            }

            var bytes = await File.ReadAllBytesAsync(filePath);
            var fileName = BuildGalleryFileName(session.Name, step);
            await _mediaSaveService.SaveImageToGalleryAsync(bytes, fileName, _options.GalleryAlbumName);
        }
    }

    public async Task<string?> ExportSessionAsZipAsync(string sessionId)
    {
        var session = await GetSessionAsync(sessionId);
        if (session == null)
        {
            return null;
        }

        // The id reaches the cache directory too, which the GetSessionAsync
        // guard above does not cover, so resolve both through the helper.
        var sessionFolder = TryGetSessionFolder(sessionId);
        if (sessionFolder == null)
        {
            return null;
        }

        var tempFolder = Path.Combine(FileSystem.CacheDirectory, $"sessioncapture_export_{sessionId}");

        if (Directory.Exists(tempFolder))
        {
            Directory.Delete(tempFolder, recursive: true);
        }

        Directory.CreateDirectory(tempFolder);

        try
        {
            var readme = GenerateReadme(session);
            await File.WriteAllTextAsync(Path.Combine(tempFolder, "README.txt"), readme, Encoding.UTF8);

            var sessionJsonPath = Path.Combine(sessionFolder, "session.json");
            if (File.Exists(sessionJsonPath))
            {
                File.Copy(sessionJsonPath, Path.Combine(tempFolder, "session.json"));
            }

            var screenshotsFolder = Path.Combine(tempFolder, "screenshots");
            Directory.CreateDirectory(screenshotsFolder);

            foreach (var step in session.Steps)
            {
                var sourcePath = Path.Combine(sessionFolder, step.ScreenshotFileName);
                if (!File.Exists(sourcePath))
                {
                    continue;
                }

                var destinationName = $"{step.StepNumber:D3}_{SanitizeFileName(step.PageName)}.jpg";
                File.Copy(sourcePath, Path.Combine(screenshotsFolder, destinationName));
            }

            var zipName = $"SessionCapture_{SanitizeFileName(session.Name)}_{session.StartedAt:yyyyMMdd_HHmmss}.zip";
            var zipPath = Path.Combine(FileSystem.CacheDirectory, zipName);
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            ZipFile.CreateFromDirectory(tempFolder, zipPath);
            return zipPath;
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                try
                {
                    Directory.Delete(tempFolder, recursive: true);
                }
                catch
                {
                }
            }
        }
    }

    public async Task ShareSessionAsync(string sessionId)
    {
        var zipPath = await ExportSessionAsZipAsync(sessionId);
        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
        {
            return;
        }

        var session = await GetSessionAsync(sessionId);
        var title = session == null ? "Session Capture" : $"Session Capture: {session.Name}";

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = title,
            File = new ShareFile(zipPath, "application/zip")
        });
    }

    /// <summary>
    /// Cancels any capture still pending from a previous session and installs a
    /// fresh token for the new one.
    /// </summary>
    private void ResetSessionToken()
    {
        var previous = _sessionCts;
        _sessionCts = new CancellationTokenSource();
        previous.Cancel();
        previous.Dispose();
    }

    private async void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        if (!IsSessionActive || !_options.AutoCaptureOnNavigation || _autoCaptureMaxReached)
        {
            return;
        }

        // Linked to the session rather than replacing a shared token: a second
        // navigation must not cancel the capture the first one is still waiting
        // to take, but ending the session must cancel both.
        using var navigationCts = CancellationTokenSource.CreateLinkedTokenSource(_sessionCts.Token);
        var token = navigationCts.Token;

        var page = Shell.Current?.CurrentPage;
        var pageName = page?.GetType().Name ?? "Unknown";
        var viewModelName = page?.BindingContext?.GetType().Name ?? "Unknown";
        var capturedAt = DateTime.UtcNow;

        try
        {
            await Task.Delay(AutoCaptureDelayMilliseconds, token);
            token.ThrowIfCancellationRequested();

            var screenshotResult = await CaptureWithoutOverlayAsync("Auto capture", token);
            if (screenshotResult == null || token.IsCancellationRequested)
            {
                return;
            }

            await SaveStepAsync(
                pageName,
                viewModelName,
                capturedAt,
                screenshotResult,
                CaptureStepType.AutoNavigation,
                userNote: null,
                token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SessionCaptureService: Auto capture failed: {ex.Message}");
        }
    }

    private async Task RecoverOrphanedSessionsAsync()
    {
        await _storageLock.WaitAsync();
        try
        {
            var sessions = await ReadIndexUnsafeAsync();
            var modified = false;

            foreach (var session in sessions.Where(session => session.EndedAt == null))
            {
                session.EndedAt = session.Steps.Count > 0
                    ? session.Steps.Max(step => step.CapturedAt)
                    : session.StartedAt;

                var sessionFolder = Path.Combine(_storageRoot, $"session_{session.Id}");
                if (!Directory.Exists(sessionFolder))
                {
                    continue;
                }

                session.Steps.Add(new CapturedStep
                {
                    StepNumber = session.Steps.Count + 1,
                    CapturedAt = session.EndedAt.Value,
                    PageName = "SessionRecovery",
                    ViewModelName = "System",
                    ScreenshotFileName = string.Empty,
                    UserNote = "Session interrupted -- application terminated during capture.",
                    Type = CaptureStepType.Annotated
                });

                var sessionFile = Path.Combine(sessionFolder, "session.json");
                var json = JsonSerializer.Serialize(session, _jsonOptions);
                await File.WriteAllTextAsync(sessionFile, json);
                modified = true;
            }

            if (modified)
            {
                await WriteIndexUnsafeAsync(sessions);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SessionCaptureService: Orphaned session recovery failed: {ex.Message}");
        }
        finally
        {
            _storageLock.Release();
        }
    }

    private async Task<IScreenshotResult?> CaptureWithoutOverlayAsync(
        string operationName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _overlayService.SuspendOverlay();
                return Task.CompletedTask;
            });

            await Task.Delay(OverlaySettleDelayMilliseconds, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            return await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    return await Screenshot.Default.CaptureAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"SessionCaptureService: {operationName} failed: {ex.Message}");
                    return null;
                }
            });
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _overlayService.ResumeOverlay();
                return Task.CompletedTask;
            });
        }
    }

    private async Task SaveStepAsync(
        string pageName,
        string viewModelName,
        DateTime capturedAt,
        IScreenshotResult screenshotResult,
        CaptureStepType type,
        string? userNote,
        CancellationToken token)
    {
        byte[] compressedBytes;

        using (var stream = await screenshotResult.OpenReadAsync())
        using (var memoryStream = new MemoryStream())
        {
            await stream.CopyToAsync(memoryStream, token);
            var rawBytes = memoryStream.ToArray();
            compressedBytes = CompressToJpeg(rawBytes, _options.CaptureQuality);
        }

        CapturedStep? step = null;

        await _storageLock.WaitAsync(token);
        try
        {
            if (_currentSession == null || _currentSessionFolder == null)
            {
                return;
            }

            if (type == CaptureStepType.AutoNavigation &&
                _currentSession.Steps.Count >= _options.MaxScreenshotsPerSession)
            {
                _autoCaptureMaxReached = true;
                return;
            }

            token.ThrowIfCancellationRequested();

            var stepNumber = _currentSession.Steps.Count + 1;

            // Named by capture time rather than step number: concurrent captures
            // are renumbered once they are all in, and a file name cannot follow.
            var fileName = $"step_{capturedAt:HHmmssfff}_{SanitizeFileName(pageName)}.jpg";
            var filePath = Path.Combine(_currentSessionFolder, fileName);

            await File.WriteAllBytesAsync(filePath, compressedBytes, token);

            step = new CapturedStep
            {
                StepNumber = stepNumber,
                CapturedAt = capturedAt,
                PageName = pageName,
                ViewModelName = viewModelName,
                ScreenshotFileName = fileName,
                UserNote = userNote,
                Type = type
            };

            // Auto-captures for different navigations run concurrently, so they
            // can finish out of order. Insert by capture time and renumber, so
            // StepNumber always reflects what the tester actually saw first.
            var insertAt = _currentSession.Steps.FindLastIndex(s => s.CapturedAt <= capturedAt) + 1;
            _currentSession.Steps.Insert(insertAt, step);
            RenumberStepsUnsafe();

            await SaveSessionJsonUnsafeAsync();
        }
        finally
        {
            _storageLock.Release();
        }

        if (step != null)
        {
            StepCaptured?.Invoke(this, step);
            _overlayService.UpdateState(true, _currentSession?.StepCount ?? step.StepNumber);
        }
    }

    /// <summary>
    /// Renumbers steps 1..n in their current (capture-time) order. Callers must
    /// already hold <see cref="_storageLock"/>.
    /// </summary>
    private void RenumberStepsUnsafe()
    {
        if (_currentSession == null)
        {
            return;
        }

        for (var i = 0; i < _currentSession.Steps.Count; i++)
        {
            _currentSession.Steps[i].StepNumber = i + 1;
        }
    }

    private byte[] CompressToJpeg(byte[] imageData, int quality)
    {
        try
        {
            quality = Math.Clamp(quality, 0, 100);

            using var inputStream = new MemoryStream(imageData);
            using var originalBitmap = SKBitmap.Decode(inputStream);
            if (originalBitmap == null)
            {
                return imageData;
            }

            using var image = SKImage.FromBitmap(originalBitmap);
            using var encodedData = image.Encode(SKEncodedImageFormat.Jpeg, quality);
            return encodedData?.ToArray() ?? imageData;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SessionCaptureService: JPEG compression failed: {ex.Message}");
            return imageData;
        }
    }

    private async Task SaveSessionJsonUnsafeAsync()
    {
        if (_currentSession == null || _currentSessionFolder == null)
        {
            return;
        }

        var sessionFile = Path.Combine(_currentSessionFolder, "session.json");
        var json = JsonSerializer.Serialize(_currentSession, _jsonOptions);
        await File.WriteAllTextAsync(sessionFile, json);
    }

    private async Task UpdateIndexUnsafeAsync(CapturedSession session)
    {
        var sessions = await ReadIndexUnsafeAsync();
        sessions.RemoveAll(existing => existing.Id == session.Id);
        sessions.Add(session);
        await WriteIndexUnsafeAsync(sessions);
    }

    private async Task PurgeOldSessionsUnsafeAsync()
    {
        var sessions = await ReadIndexUnsafeAsync();
        if (sessions.Count <= _options.MaxSessionsRetained)
        {
            return;
        }

        foreach (var session in sessions.OrderBy(session => session.StartedAt)
                     .Take(sessions.Count - _options.MaxSessionsRetained)
                     .ToList())
        {
            var folder = Path.Combine(_storageRoot, $"session_{session.Id}");
            if (Directory.Exists(folder))
            {
                try
                {
                    Directory.Delete(folder, recursive: true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"SessionCaptureService: Failed to purge session {session.Id}: {ex.Message}");
                }
            }

            sessions.Remove(session);
        }

        await WriteIndexUnsafeAsync(sessions);
    }

    private async Task<List<CapturedSession>> ReadIndexUnsafeAsync()
    {
        if (!File.Exists(_indexFilePath))
        {
            return new List<CapturedSession>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_indexFilePath);
            return JsonSerializer.Deserialize<List<CapturedSession>>(json, _jsonOptions)
                   ?? new List<CapturedSession>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SessionCaptureService: Failed to read index.json: {ex.Message}");
            return new List<CapturedSession>();
        }
    }

    private async Task WriteIndexUnsafeAsync(List<CapturedSession> sessions)
    {
        var json = JsonSerializer.Serialize(sessions, _jsonOptions);
        await File.WriteAllTextAsync(_indexFilePath, json);
    }

    private string GenerateReadme(CapturedSession session)
    {
        var builder = new StringBuilder();
        builder.AppendLine("SESSION CAPTURE REPORT");
        builder.AppendLine("======================");
        builder.AppendLine();
        builder.AppendLine($"Session:    {session.Name}");
        builder.AppendLine($"Date:       {session.StartedAt:yyyy-MM-dd HH:mm}");
        builder.AppendLine(session.Duration.HasValue
            ? $"Duration:   {(int)session.Duration.Value.TotalMinutes}m {session.Duration.Value.Seconds}s"
            : "Duration:   (interrupted)");
        builder.AppendLine($"Steps:      {session.StepCount}");
        builder.AppendLine();
        builder.AppendLine($"Device:     {session.DeviceModel}");
        builder.AppendLine($"OS:         {session.OsVersion}");
        builder.AppendLine($"App:        v{session.AppVersion}");
        if (!string.IsNullOrWhiteSpace(session.TesterName))
        {
            builder.AppendLine($"Tester:     {session.TesterName}");
        }

        builder.AppendLine();
        builder.AppendLine("STEPS");
        builder.AppendLine("-----");
        builder.AppendLine();
        builder.AppendLine(" #  | Time     | Page              | Type     | Note");
        builder.AppendLine("----|----------|-------------------|----------|---------------------------");

        foreach (var step in session.Steps)
        {
            var typeLabel = step.Type switch
            {
                CaptureStepType.AutoNavigation => "auto",
                CaptureStepType.ManualCapture => "manual",
                CaptureStepType.Annotated => "annotated",
                _ => "unknown"
            };

            var pageName = step.PageName.Length > 17 ? step.PageName[..17] : step.PageName.PadRight(17);
            builder.AppendLine(
                $" {step.StepNumber,-3}| {step.CapturedAt:HH:mm:ss} | {pageName} | {typeLabel,-8} | {step.UserNote ?? string.Empty}");
        }

        builder.AppendLine();
        builder.AppendLine("Generated by SessionCapture.Maui");
        return builder.ToString();
    }

    private static string BuildGalleryFileName(string sessionName, CapturedStep step)
    {
        var fileName = $"{SanitizeFileName(sessionName)}_{step.StepNumber:D3}_{SanitizeFileName(step.PageName)}";
        if (step.Type == CaptureStepType.Annotated && !string.IsNullOrWhiteSpace(step.UserNote))
        {
            var note = SanitizeFileName(step.UserNote);
            if (note.Length > 40)
            {
                note = note[..40];
            }

            fileName += $"_note_{note}";
        }

        return $"{fileName}.jpg";
    }

    private static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "unnamed";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(input.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray());
        return sanitized.Trim('_');
    }

    private void EnsureEnabled()
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("Session capture is disabled.");
        }
    }
}
