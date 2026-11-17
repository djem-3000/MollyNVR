using MollyNvr.Application.Recording;

namespace MollyNvr.Application.Archive;

/// <summary>Склейка сегментов в интервалы и расчёт карты времени воспроизведения.</summary>
public static class ArchiveTimelineBuilder
{
    /// <summary>Допустимый разрыв между сегментами, который считаем стыком, а не пропуском.</summary>
    public static readonly TimeSpan SegmentGap = TimeSpan.FromSeconds(10);

    /// <summary>Склеивает соседние сегменты в непрерывные интервалы.</summary>
    public static IReadOnlyList<ArchiveInterval> Merge(
        IEnumerable<RecordingSegment> segments,
        TimeSpan? gap = null)
    {
        var tolerance = gap ?? SegmentGap;
        var result = new List<ArchiveInterval>();

        foreach (var segment in segments.OrderBy(static item => item.StartedAt))
        {
            if (segment.EndedAt <= segment.StartedAt)
            {
                continue;
            }

            if (result.Count > 0 && segment.StartedAt <= result[^1].End + tolerance)
            {
                var last = result[^1];
                result[^1] = last with
                {
                    End = last.End > segment.EndedAt ? last.End : segment.EndedAt,
                    SizeBytes = last.SizeBytes + segment.SizeBytes,
                };

                continue;
            }

            result.Add(new ArchiveInterval(segment.StartedAt, segment.EndedAt, segment.SizeBytes));
        }

        return result;
    }

    /// <summary>Обрезает события по границам интервала; открытые события завершаются текущим моментом.</summary>
    public static IReadOnlyList<ArchiveEventSlice> ClipEvents(
        IEnumerable<RecordingEvent> events,
        DateTimeOffset from,
        DateTimeOffset to,
        DateTimeOffset now)
    {
        var result = new List<ArchiveEventSlice>();

        foreach (var recordingEvent in events)
        {
            var start = recordingEvent.StartedAt < from ? from : recordingEvent.StartedAt;
            var rawEnd = recordingEvent.EndedAt ?? now;
            var end = rawEnd > to ? to : rawEnd;

            if (end <= start)
            {
                continue;
            }

            result.Add(new ArchiveEventSlice(recordingEvent.Type, start, end));
        }

        return result.OrderBy(static item => item.Start).ToList();
    }

    /// <summary>
    /// Строит карту «реальное время → время в склейке». Конец сегмента берётся из начала
    /// следующего файла: так накопленная ошибка не растёт от сегмента к сегменту.
    /// </summary>
    public static IReadOnlyList<ArchiveSegmentSlice> BuildPlaybackSegments(
        IReadOnlyList<RecordingSegment> segments,
        TimeSpan? gap = null)
    {
        var tolerance = gap ?? SegmentGap;
        var ordered = segments
            .Where(static segment => segment.EndedAt > segment.StartedAt)
            .OrderBy(static segment => segment.StartedAt)
            .ToList();
        var result = new List<ArchiveSegmentSlice>(ordered.Count);
        var media = 0d;

        for (var index = 0; index < ordered.Count; index++)
        {
            var segment = ordered[index];
            var hasNeighbour = index + 1 < ordered.Count &&
                               ordered[index + 1].StartedAt <= segment.EndedAt + tolerance;
            var end = hasNeighbour ? ordered[index + 1].StartedAt : segment.EndedAt;

            if (end <= segment.StartedAt)
            {
                end = segment.StartedAt.AddMilliseconds(500);
            }

            var duration = (end - segment.StartedAt).TotalSeconds;
            result.Add(new ArchiveSegmentSlice(segment.StartedAt, end, media, media + duration));
            media += duration;
        }

        return result;
    }
}
