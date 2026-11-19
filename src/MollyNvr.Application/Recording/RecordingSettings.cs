namespace MollyNvr.Application.Recording;

/// <summary>Camera recording settings: stream, modes, schedules, events, pre/post-roll.</summary>
public sealed record RecordingSettings
{
    public Guid CameraId { get; init; }

        /// <summary>Master switch for camera recording.</summary>
    public bool Enabled { get; init; }

        /// <summary>Identifier of the camera profile to record from (main/sub).</summary>
    public string ProfileId { get; init; } = string.Empty;

        /// <summary>Continuous recording on a schedule.</summary>
    public bool ContinuousEnabled { get; init; }

    public WeeklySchedule ContinuousSchedule { get; init; } = WeeklySchedule.Empty;

        /// <summary>Recording triggered by camera events.</summary>
    public bool EventEnabled { get; init; }

        /// <summary>When events are monitored (no recording outside these windows).</summary>
    public WeeklySchedule EventSchedule { get; init; } = WeeklySchedule.Empty;

        /// <summary>Camera event types that trigger recording.</summary>
    public IReadOnlyList<string> EventTypes { get; init; } = [];

        /// <summary>How many seconds to keep before the event.</summary>
    public int PreRollSeconds { get; init; }

        /// <summary>How many seconds to keep recording after the event ends.</summary>
    public int PostRollSeconds { get; init; }

        /// <summary>Whether at least one mode is enabled.</summary>
    public bool HasActiveMode => Enabled && (ContinuousEnabled || EventEnabled);

        /// <summary>Whether the recording process must be running at this moment (pre-roll buffer).</summary>
    public bool ShouldRecord(DateTimeOffset moment) =>
        Enabled &&
        ((ContinuousEnabled && ContinuousSchedule.IsActive(moment)) ||
         (EventEnabled && EventSchedule.IsActive(moment)));
}

/// <summary>Overwrite mode of the shared pool.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum StorageOverwriteMode
{
        /// <summary>Ring buffer: the oldest segment is deleted as soon as the quota is exceeded.</summary>
    Fifo = 0,

        /// <summary>Watermarks: when high is reached, delete old data down to low.</summary>
    Watermark = 1,
}

/// <summary>Shared recording pool settings.</summary>
public sealed record StorageSettings
{
        /// <summary>Pool directory (inside the mounted volume).</summary>
    public string Path { get; init; } = string.Empty;

        /// <summary>Quota — share of the volume capacity in percent (90 by default).</summary>
    public int QuotaPercent { get; init; } = 90;

    public StorageOverwriteMode Mode { get; init; } = StorageOverwriteMode.Fifo;

        /// <summary>High watermark (percent of the quota) that starts cleanup.</summary>
    public int HighWatermarkPercent { get; init; } = 90;

        /// <summary>Low watermark (percent of the quota) to clean down to.</summary>
    public int LowWatermarkPercent { get; init; } = 80;
}

/// <summary>Recording segment on disk.</summary>
public sealed record RecordingSegment(
    Guid Id,
    Guid CameraId,
    string Path,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    long SizeBytes);

/// <summary>Camera event that triggers recording (for storing recording windows).</summary>
public sealed record RecordingEvent(
    Guid Id,
    Guid CameraId,
    string Type,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt);

/// <summary>Pool state: how much is used and how much is allowed.</summary>
public sealed record StoragePoolStatus(
    string Path,
    long TotalBytes,
    long FreeBytes,
    long QuotaBytes,
    long UsedBytes)
{
    public long AvailableBytes => Math.Max(0, QuotaBytes - UsedBytes);

    public int UsedPercent => QuotaBytes <= 0
        ? 0
        : (int)Math.Clamp(UsedBytes * 100 / QuotaBytes, 0, 100);
}
