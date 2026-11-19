using MollyNvr.Application.Recording;

namespace MollyNvr.Application.Archive;

/// <summary>Stitches segments into intervals and builds the playback time map.</summary>
public static class ArchiveTimelineBuilder
{
        /// <summary>Allowed gap between segments treated as a joint rather than a gap.</summary>
    public static readonly TimeSpan SegmentGap = TimeSpan.FromSeconds(10);

        /// <summary>Stitches adjacent segments into continuous intervals.</summary>
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

        /// <summary>Clips events by the interval boundaries; open events end at the current moment.</summary>
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
        /// Builds the "real time -> stitched time" map. A segment end is taken from the start of
        /// the next file, so the accumulated error does not grow from segment to segment.
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
