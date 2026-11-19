namespace MollyNvr.Application.Archive;

/// <summary>Camera in the archive list.</summary>
public sealed record ArchiveCamera(
    Guid Id,
    string Name,
    bool IsActive,
    bool HasRecordingsToday);

/// <summary>Calendar day that has recordings.</summary>
public sealed record ArchiveDay(
    DateOnly Date,
    int SegmentCount,
    long SizeBytes);

/// <summary>Days of the month that have recordings.</summary>
public sealed record ArchiveCalendar(
    Guid CameraId,
    int Year,
    int Month,
    IReadOnlyList<ArchiveDay> Days);

/// <summary>Event type for the timeline filter.</summary>
public sealed record ArchiveEventType(
    string Key,
    string Label);

/// <summary>Continuous recording interval (several adjacent segments).</summary>
public sealed record ArchiveInterval(
    DateTimeOffset Start,
    DateTimeOffset End,
    long SizeBytes);

/// <summary>Event window clipped by the day boundaries.</summary>
public sealed record ArchiveEventSlice(
    string Type,
    DateTimeOffset Start,
    DateTimeOffset End);

/// <summary>Timeline data for one day.</summary>
public sealed record ArchiveTimeline(
    Guid CameraId,
    DateOnly Date,
    IReadOnlyList<ArchiveInterval> Intervals,
    IReadOnlyList<ArchiveEventSlice> Events,
    IReadOnlyList<ArchiveEventType> EventTypes);

/// <summary>Segment in a playback session: real time and position in the continuous stream.</summary>
public sealed record ArchiveSegmentSlice(
    DateTimeOffset Start,
    DateTimeOffset End,
    double MediaStart,
    double MediaEnd);

/// <summary>Started playback session.</summary>
public sealed record ArchivePlayback(
    Guid SessionId,
    string PlaylistUrl,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    double StartOffsetSeconds,
    double DurationSeconds,
    bool Transcoded,
    IReadOnlyList<ArchiveSegmentSlice> Segments);
