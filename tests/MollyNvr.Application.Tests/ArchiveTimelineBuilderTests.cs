using MollyNvr.Application.Archive;
using MollyNvr.Application.Recording;

namespace MollyNvr.Application.Tests;

public class ArchiveTimelineBuilderTests
{
    private static DateTimeOffset At(int hour, int minute = 0, int second = 0) =>
        new(2026, 11, 9, hour, minute, second, TimeSpan.Zero);

    private static RecordingSegment Segment(
        DateTimeOffset start,
        DateTimeOffset end,
        long size = 100,
        Guid? id = null) =>
        new(id ?? Guid.NewGuid(), Guid.NewGuid(), $"/recordings/{start:HHmmss}.mp4", start, end, size);

    [Fact]
    public void Merge_JoinsSegmentsWithSmallGap()
    {
        var segments = new[]
        {
            Segment(At(10), At(10, 5)),
            Segment(At(10, 5, 3), At(10, 10), 200),
        };

        var intervals = ArchiveTimelineBuilder.Merge(segments);

        var interval = Assert.Single(intervals);
        Assert.Equal(At(10), interval.Start);
        Assert.Equal(At(10, 10), interval.End);
        Assert.Equal(300, interval.SizeBytes);
    }

    [Fact]
    public void Merge_KeepsRealGapSeparate()
    {
        var segments = new[]
        {
            Segment(At(10), At(10, 5)),
            Segment(At(10, 10), At(10, 15)),
        };

        var intervals = ArchiveTimelineBuilder.Merge(segments);

        Assert.Equal(2, intervals.Count);
    }

    [Fact]
    public void Merge_SortsByStartAndIgnoresEmptySegments()
    {
        var segments = new[]
        {
            Segment(At(11), At(11, 5)),
            Segment(At(10), At(10)),
            Segment(At(9), At(9, 5)),
        };

        var intervals = ArchiveTimelineBuilder.Merge(segments);

        Assert.Equal(2, intervals.Count);
        Assert.Equal(At(9), intervals[0].Start);
        Assert.Equal(At(11), intervals[1].Start);
    }

    [Fact]
    public void ClipEvents_ClipsBoundsAndClosesOpenEvent()
    {
        var events = new[]
        {
            new RecordingEvent(Guid.NewGuid(), Guid.NewGuid(), "motion", At(9, 50), At(10, 1)),
            new RecordingEvent(Guid.NewGuid(), Guid.NewGuid(), "person", At(11), null),
        };

        var slices = ArchiveTimelineBuilder.ClipEvents(
            events,
            At(10),
            At(12),
            At(11, 30));

        Assert.Equal(2, slices.Count);
        Assert.Equal(At(10), slices[0].Start);
        Assert.Equal(At(10, 1), slices[0].End);
        Assert.Equal(At(11), slices[1].Start);
        Assert.Equal(At(11, 30), slices[1].End);
    }

    [Fact]
    public void ClipEvents_DropsEventsOutsideInterval()
    {
        var events = new[]
        {
            new RecordingEvent(Guid.NewGuid(), Guid.NewGuid(), "motion", At(8), At(9)),
            new RecordingEvent(Guid.NewGuid(), Guid.NewGuid(), "motion", At(13), At(14)),
        };

        var slices = ArchiveTimelineBuilder.ClipEvents(events, At(10), At(12), At(12));

        Assert.Empty(slices);
    }

    [Fact]
    public void BuildPlaybackSegments_UsesNextStartToAvoidDrift()
    {
        var segments = new[]
        {
            Segment(At(10), At(10, 5, 2)),
            Segment(At(10, 5), At(10, 10, 1)),
        };

        var slices = ArchiveTimelineBuilder.BuildPlaybackSegments(segments);

        Assert.Equal(2, slices.Count);
        Assert.Equal(0, slices[0].MediaStart, 3);
        Assert.Equal(300, slices[0].MediaEnd, 3);
        Assert.Equal(300, slices[1].MediaStart, 3);
        Assert.Equal(601, slices[1].MediaEnd, 3);
        Assert.Equal(At(10, 10, 1), slices[1].End);
    }

    [Fact]
    public void BuildPlaybackSegments_KeepsSeparateRecordingsAndCumulativeMedia()
    {
        var segments = new[]
        {
            Segment(At(10), At(10, 1)),
            Segment(At(10, 30), At(10, 31)),
        };

        var slices = ArchiveTimelineBuilder.BuildPlaybackSegments(segments);

        Assert.Equal(2, slices.Count);
        Assert.Equal(60, slices[0].MediaEnd, 3);
        Assert.Equal(60, slices[1].MediaStart, 3);
        Assert.Equal(120, slices[1].MediaEnd, 3);
    }
}
