using MollyNvr.Application.Recording;

namespace MollyNvr.Application.Archive;

/// <summary>Reading the recording index for the archive.</summary>
public interface IArchiveStore
{
        /// <summary>Camera segments intersecting the interval, ordered by start time.</summary>
    Task<IReadOnlyList<RecordingSegment>> SegmentsAsync(
        Guid cameraId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);

        /// <summary>Camera events intersecting the interval, ordered by start time.</summary>
    Task<IReadOnlyList<RecordingEvent>> EventsAsync(
        Guid cameraId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);

        /// <summary>Cameras that have recordings in the interval.</summary>
    Task<IReadOnlyList<Guid>> CameraIdsWithRecordingsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);
}

/// <summary>Source segment file for a playback session.</summary>
public sealed record PlaybackSourceFile(string Path);

/// <summary>Playback session prepared by the manager.</summary>
public sealed record ArchivePlaybackSession(Guid Id, string Directory);

/// <summary>HLS file served by the API.</summary>
public sealed record ArchivePlaybackFile(string ContentType, byte[] Content);

/// <summary>How video is served over HLS.</summary>
public enum ArchiveVideoMode
{
        /// <summary>Copy H.264 without transcoding.</summary>
    Copy,

        /// <summary>Copy H.265 with the hvc1 tag (the browser supports HEVC).</summary>
    CopyHevc,

        /// <summary>Transcode to H.264.</summary>
    Transcode,
}

/// <summary>Preparation and shutdown of archive HLS playback sessions.</summary>
public interface IArchivePlaybackManager
{
    Task<ArchivePlaybackSession> StartAsync(
        IReadOnlyList<PlaybackSourceFile> files,
        ArchiveVideoMode mode,
        CancellationToken cancellationToken);

    ArchivePlaybackFile? FindFile(Guid sessionId, string fileName);

    void Stop(Guid sessionId);
}
