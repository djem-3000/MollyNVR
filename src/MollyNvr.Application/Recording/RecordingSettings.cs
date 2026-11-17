namespace MollyNvr.Application.Recording;

/// <summary>Настройки записи камеры: поток, режимы, расписания, события, pre/post-roll.</summary>
public sealed record RecordingSettings
{
    public Guid CameraId { get; init; }

    /// <summary>Общий выключатель записи камеры.</summary>
    public bool Enabled { get; init; }

    /// <summary>Идентификатор профиля камеры, с которого пишем (main/sub).</summary>
    public string ProfileId { get; init; } = string.Empty;

    /// <summary>Потоковая запись по расписанию.</summary>
    public bool ContinuousEnabled { get; init; }

    public WeeklySchedule ContinuousSchedule { get; init; } = WeeklySchedule.Empty;

    /// <summary>Запись по событиям камеры.</summary>
    public bool EventEnabled { get; init; }

    /// <summary>Когда следим за событиями (вне этих окон запись не ведётся).</summary>
    public WeeklySchedule EventSchedule { get; init; } = WeeklySchedule.Empty;

    /// <summary>Типы событий камеры, по которым пишем.</summary>
    public IReadOnlyList<string> EventTypes { get; init; } = [];

    /// <summary>Сколько секунд до события сохранить.</summary>
    public int PreRollSeconds { get; init; }

    /// <summary>Сколько секунд продолжать запись после окончания события.</summary>
    public int PostRollSeconds { get; init; }

    /// <summary>Включён ли хотя бы один режим.</summary>
    public bool HasActiveMode => Enabled && (ContinuousEnabled || EventEnabled);

    /// <summary>Нужно ли держать процесс записи запущенным в этот момент (буфер для pre-roll).</summary>
    public bool ShouldRecord(DateTimeOffset moment) =>
        Enabled &&
        ((ContinuousEnabled && ContinuousSchedule.IsActive(moment)) ||
         (EventEnabled && EventSchedule.IsActive(moment)));
}

/// <summary>Режим перезаписи общего пула.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum StorageOverwriteMode
{
    /// <summary>Кольцевой буфер: удаляется самый старый сегмент, как только превышена квота.</summary>
    Fifo = 0,

    /// <summary>Водяные знаки: при достижении high удаляем старое до low.</summary>
    Watermark = 1,
}

/// <summary>Настройки общего пула записей.</summary>
public sealed record StorageSettings
{
    /// <summary>Каталог пула (внутри смонтированного тома).</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Квота — доля объёма тома в процентах (по умолчанию 90).</summary>
    public int QuotaPercent { get; init; } = 90;

    public StorageOverwriteMode Mode { get; init; } = StorageOverwriteMode.Fifo;

    /// <summary>Верхний порог (в процентах от квоты), после которого начинается очистка.</summary>
    public int HighWatermarkPercent { get; init; } = 90;

    /// <summary>Нижний порог (в процентах от квоты), до которого очищаем.</summary>
    public int LowWatermarkPercent { get; init; } = 80;
}

/// <summary>Сегмент записи на диске.</summary>
public sealed record RecordingSegment(
    Guid Id,
    Guid CameraId,
    string Path,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    long SizeBytes);

/// <summary>Событие камеры, по которому пишем (для хранения окон записи).</summary>
public sealed record RecordingEvent(
    Guid Id,
    Guid CameraId,
    string Type,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt);

/// <summary>Состояние пула: сколько занято и сколько разрешено.</summary>
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
