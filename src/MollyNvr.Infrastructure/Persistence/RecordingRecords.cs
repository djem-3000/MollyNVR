namespace MollyNvr.Infrastructure.Persistence;

/// <summary>Настройки записи камеры (одна строка на камеру).</summary>
public sealed class RecordingSettingsRecord
{
    public Guid CameraId { get; set; }

    public bool Enabled { get; set; }

    public string ProfileId { get; set; } = string.Empty;

    public bool ContinuousEnabled { get; set; }

    /// <summary>336 символов «0»/«1»: 7 дней × 48 получасовых слотов.</summary>
    public string ContinuousSchedule { get; set; } = string.Empty;

    public bool EventEnabled { get; set; }

    public string EventSchedule { get; set; } = string.Empty;

    /// <summary>Ключи типов событий через запятую.</summary>
    public string EventTypes { get; set; } = string.Empty;

    public int PreRollSeconds { get; set; }

    public int PostRollSeconds { get; set; }
}

/// <summary>Настройки общего пула записей (единственная строка с Id = 1).</summary>
public sealed class StorageSettingsRecord
{
    public int Id { get; set; } = 1;

    public string Path { get; set; } = string.Empty;

    public int QuotaPercent { get; set; } = 90;

    public int Mode { get; set; }

    public int HighWatermarkPercent { get; set; } = 90;

    public int LowWatermarkPercent { get; set; } = 80;
}

/// <summary>Сегмент записи на диске (для учёта места и очистки). Время хранится в UTC.</summary>
public sealed class RecordingSegmentRecord
{
    public Guid Id { get; set; }

    public Guid CameraId { get; set; }

    public string Path { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; }

    public DateTime EndedAt { get; set; }

    public long SizeBytes { get; set; }
}

/// <summary>Событие камеры: окно записи вокруг обнаружения. Время хранится в UTC.</summary>
public sealed class RecordingEventRecord
{
    public Guid Id { get; set; }

    public Guid CameraId { get; set; }

    public string Type { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }
}
