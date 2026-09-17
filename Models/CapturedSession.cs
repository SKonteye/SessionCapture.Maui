namespace SessionCapture.Maui.Models;

public sealed class CapturedSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public string DeviceModel { get; set; } = string.Empty;

    public string OsVersion { get; set; } = string.Empty;

    public string AppVersion { get; set; } = string.Empty;

    public string? TesterName { get; set; }

    public List<CapturedStep> Steps { get; set; } = new();

    public TimeSpan? Duration => EndedAt.HasValue ? EndedAt.Value - StartedAt : null;

    public int StepCount => Steps.Count;

    public bool IsActive => !EndedAt.HasValue;
}
