namespace MollyNvr.Infrastructure.Persistence;

/// <summary>Camera recording settings (one row per camera).</summary>
public sealed class RecordingSettingsRecord
{
    public Guid CameraId { get; set; }

    public bool Enabled { get; set; }

    public string ProfileId { get; set; } = string.Empty;

    public bool ContinuousEnabled { get; set; }

        /// <summary>336 '0'/'1' characters: 7 days x 48 half-hour slots.</summary>
    public string ContinuousSchedule { get; set; } = string.Empty;

    public bool EventEnabled { get; set; }

    public string EventSchedule { get; set; } = string.Empty;

        /// <summary>Event type keys separated by commas.</summary>
    public string EventTypes { get; set; } = string.Empty;

    public int PreRollSeconds { get; set; }

    public int PostRollSeconds { get; set; }
}

/// <summary>Shared recording pool settings (the only row with Id = 1).</summary>
public sealed class StorageSettingsRecord
{
    public int Id { get; set; } = 1;

    public string Path { get; set; } = string.Empty;

    public int QuotaPercent { get; set; } = 90;

    public int Mode { get; set; }

    public int HighWatermarkPercent { get; set; } = 90;

    public int LowWatermarkPercent { get; set; } = 80;
}

/// <summary>Recording segment on disk (for space accounting and cleanup). Times are stored in UTC.</summary>
public sealed class RecordingSegmentRecord
{
    public Guid Id { get; set; }

    public Guid CameraId { get; set; }

    public string Path { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; }

    public DateTime EndedAt { get; set; }

    public long SizeBytes { get; set; }
}

/// <summary>Camera event: the recording window around a detection. Times are stored in UTC.</summary>
public sealed class RecordingEventRecord
{
    public Guid Id { get; set; }

    public Guid CameraId { get; set; }

    public string Type { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }
}
