namespace MollyNvr.Application.Archive;

/// <summary>Камера в списке архива.</summary>
public sealed record ArchiveCamera(
    Guid Id,
    string Name,
    bool IsActive,
    bool HasRecordingsToday);

/// <summary>День календаря, в который есть записи.</summary>
public sealed record ArchiveDay(
    DateOnly Date,
    int SegmentCount,
    long SizeBytes);

/// <summary>Дни месяца, в которые есть записи.</summary>
public sealed record ArchiveCalendar(
    Guid CameraId,
    int Year,
    int Month,
    IReadOnlyList<ArchiveDay> Days);

/// <summary>Тип события для фильтра таймлайна.</summary>
public sealed record ArchiveEventType(
    string Key,
    string Label);

/// <summary>Непрерывный интервал записи (несколько соседних сегментов).</summary>
public sealed record ArchiveInterval(
    DateTimeOffset Start,
    DateTimeOffset End,
    long SizeBytes);

/// <summary>Окно события, обрезанное по границам дня.</summary>
public sealed record ArchiveEventSlice(
    string Type,
    DateTimeOffset Start,
    DateTimeOffset End);

/// <summary>Данные таймлайна за сутки.</summary>
public sealed record ArchiveTimeline(
    Guid CameraId,
    DateOnly Date,
    IReadOnlyList<ArchiveInterval> Intervals,
    IReadOnlyList<ArchiveEventSlice> Events,
    IReadOnlyList<ArchiveEventType> EventTypes);

/// <summary>Сегмент в playback-сессии: реальное время и позиция в непрерывном потоке.</summary>
public sealed record ArchiveSegmentSlice(
    DateTimeOffset Start,
    DateTimeOffset End,
    double MediaStart,
    double MediaEnd);

/// <summary>Запущенная playback-сессия.</summary>
public sealed record ArchivePlayback(
    Guid SessionId,
    string PlaylistUrl,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    double StartOffsetSeconds,
    double DurationSeconds,
    bool Transcoded,
    IReadOnlyList<ArchiveSegmentSlice> Segments);
