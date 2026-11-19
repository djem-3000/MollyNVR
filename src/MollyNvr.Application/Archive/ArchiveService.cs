using MollyNvr.Application.Cameras;
using MollyNvr.Application.Recording;
using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Application.Archive;

/// <summary>Calendar, timeline and archive playback start.</summary>
public sealed class ArchiveService(
    IArchiveStore store,
    ICameraRepository cameraRepository,
    IRecordingSettingsStore settingsStore,
    IArchivePlaybackManager playback)
{
    private static readonly TimeSpan PlaybackWindow = TimeSpan.FromHours(2);
    private static readonly TimeSpan SegmentSearch = TimeSpan.FromMinutes(15);

    public async Task<IReadOnlyList<ArchiveCamera>> ListCamerasAsync(CancellationToken cancellationToken)
    {
        var cameras = await cameraRepository.ListAsync(cancellationToken);
        var dayStart = StartOfDay(DateOnly.FromDateTime(DateTime.Now));
        var withRecordings = (await store.CameraIdsWithRecordingsAsync(
                dayStart,
                dayStart.AddDays(1),
                cancellationToken))
            .ToHashSet();

        return cameras
            .Select(camera => new ArchiveCamera(
                camera.Id,
                camera.Name,
                camera.IsActive,
                withRecordings.Contains(camera.Id)))
            .ToList();
    }

    public async Task<ArchiveCalendar> GetCalendarAsync(
        Guid cameraId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        if (year is < 2000 or > 2100)
        {
                        throw new ArgumentException("Year is out of the allowed range.", nameof(year));
        }

        if (month is < 1 or > 12)
        {
                        throw new ArgumentException("Month must be between 1 and 12.", nameof(month));
        }

        await RequireCameraAsync(cameraId, cancellationToken);

        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1);
        var segments = await store.SegmentsAsync(
            cameraId,
            StartOfDay(from),
            StartOfDay(to),
            cancellationToken);

        var days = segments
            .GroupBy(segment => DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(segment.StartedAt, TimeZoneInfo.Local).DateTime))
            .OrderBy(group => group.Key)
            .Select(group => new ArchiveDay(
                group.Key,
                group.Count(),
                group.Sum(static segment => segment.SizeBytes)))
            .ToList();

        return new ArchiveCalendar(cameraId, year, month, days);
    }

    public async Task<ArchiveTimeline> GetTimelineAsync(
        Guid cameraId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        await RequireCameraAsync(cameraId, cancellationToken);

        var from = StartOfDay(date);
        var to = from.AddDays(1);
        var segments = await store.SegmentsAsync(cameraId, from, to, cancellationToken);
        var events = await store.EventsAsync(cameraId, from, to, cancellationToken);

        var eventSlices = ArchiveTimelineBuilder.ClipEvents(events, from, to, DateTimeOffset.Now);
        var eventTypes = eventSlices
            .Select(static item => item.Type)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static type => type, StringComparer.OrdinalIgnoreCase)
            .Select(static type => new ArchiveEventType(type, ArchiveEventLabels.Label(type)))
            .ToList();

        return new ArchiveTimeline(
            cameraId,
            date,
            ArchiveTimelineBuilder.Merge(segments),
            eventSlices,
            eventTypes);
    }

    public async Task<ArchivePlayback> CreatePlaybackAsync(
        Guid cameraId,
        DateTimeOffset from,
        bool supportsHevc,
        CancellationToken cancellationToken)
    {
        var camera = await RequireCameraAsync(cameraId, cancellationToken);
        var localFrom = TimeZoneInfo.ConvertTime(from, TimeZoneInfo.Local);
        var dayStart = StartOfDay(DateOnly.FromDateTime(localFrom.DateTime));
        var dayEnd = dayStart.AddDays(1);
        var windowEnd = from.Add(PlaybackWindow);
        if (windowEnd > dayEnd)
        {
            windowEnd = dayEnd;
        }

        var candidates = await LoadExistingAsync(
            cameraId,
            from.Subtract(SegmentSearch),
            windowEnd,
            cancellationToken);

        var first = candidates.FirstOrDefault(segment => segment.EndedAt > from);

        if (first is null)
        {
                        // The point falls into a gap: find the nearest recording before the end of the day.
            candidates = await LoadExistingAsync(cameraId, from, dayEnd, cancellationToken);
            first = candidates.FirstOrDefault(segment => segment.EndedAt > from);

            if (first is null)
            {
                                throw new ArgumentException("There are no recordings at the selected time.", nameof(from));
            }

            windowEnd = first.StartedAt.Add(PlaybackWindow);
            if (windowEnd > dayEnd)
            {
                windowEnd = dayEnd;
            }
        }

        var selected = candidates
            .Where(segment => segment.StartedAt < windowEnd && segment.EndedAt > first.StartedAt)
            .ToList();
        var slices = ArchiveTimelineBuilder.BuildPlaybackSegments(selected);

        if (slices.Count == 0)
        {
                        throw new ArgumentException("There are no recordings at the selected time.", nameof(from));
        }

        var windowStart = slices[0].Start;
        var totalDuration = slices[^1].MediaEnd;
        var startOffset = Math.Clamp(
            (from - windowStart).TotalSeconds,
            0,
            Math.Max(0, totalDuration - 0.5));

        var settings = await settingsStore.FindAsync(cameraId, cancellationToken);
        var profileId = settings?.ProfileId ?? camera.MainProfileId;
        var profile = camera.Profiles.FirstOrDefault(item =>
            string.Equals(item.Id, profileId, StringComparison.Ordinal));
        var codec = VideoCodecNames.Normalize(profile?.Video.Codec);
        var mode = codec switch
        {
            VideoCodecNames.H264 => ArchiveVideoMode.Copy,
            VideoCodecNames.H265 => supportsHevc ? ArchiveVideoMode.CopyHevc : ArchiveVideoMode.Transcode,
            VideoCodecNames.Mjpeg => ArchiveVideoMode.Transcode,
            _ => ArchiveVideoMode.Copy,
        };

        var files = selected
            .Select(segment => new PlaybackSourceFile(segment.Path))
            .ToList();

        var session = await playback.StartAsync(files, mode, cancellationToken);

        return new ArchivePlayback(
            session.Id,
            $"/api/archive/playback/{session.Id}/index.m3u8",
            windowStart,
            slices[^1].End,
            startOffset,
            totalDuration,
            mode == ArchiveVideoMode.Transcode,
            slices);
    }

    public void StopPlayback(Guid sessionId) => playback.Stop(sessionId);

    public ArchivePlaybackFile? FindPlaybackFile(Guid sessionId, string fileName) =>
        playback.FindFile(sessionId, fileName);

    private async Task<IReadOnlyList<RecordingSegment>> LoadExistingAsync(
        Guid cameraId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var segments = await store.SegmentsAsync(cameraId, from, to, cancellationToken);

        return segments
            .Where(static segment => FileExists(segment.Path))
            .OrderBy(static segment => segment.StartedAt)
            .ToList();
    }

    private async Task<MollyNvr.Domain.Cameras.Camera> RequireCameraAsync(
        Guid cameraId,
        CancellationToken cancellationToken) =>
        await cameraRepository.FindAsync(cameraId, cancellationToken)
        ?? throw new CameraNotFoundException(cameraId);

    private static bool FileExists(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static DateTimeOffset StartOfDay(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }
}
