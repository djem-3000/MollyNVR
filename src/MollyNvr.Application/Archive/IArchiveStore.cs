using MollyNvr.Application.Recording;

namespace MollyNvr.Application.Archive;

/// <summary>Чтение индекса записей для архива.</summary>
public interface IArchiveStore
{
    /// <summary>Сегменты камеры, пересекающиеся с интервалом, по возрастанию времени начала.</summary>
    Task<IReadOnlyList<RecordingSegment>> SegmentsAsync(
        Guid cameraId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);

    /// <summary>События камеры, пересекающиеся с интервалом, по возрастанию времени начала.</summary>
    Task<IReadOnlyList<RecordingEvent>> EventsAsync(
        Guid cameraId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);

    /// <summary>Камеры, у которых есть записи в интервале.</summary>
    Task<IReadOnlyList<Guid>> CameraIdsWithRecordingsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);
}

/// <summary>Файл исходного сегмента для playback-сессии.</summary>
public sealed record PlaybackSourceFile(string Path);

/// <summary>Сессия воспроизведения, подготовленная менеджером.</summary>
public sealed record ArchivePlaybackSession(Guid Id, string Directory);

/// <summary>Файл HLS, который отдаёт API.</summary>
public sealed record ArchivePlaybackFile(string ContentType, byte[] Content);

/// <summary>Как отдавать видео в HLS.</summary>
public enum ArchiveVideoMode
{
    /// <summary>Копировать H.264 без перекодирования.</summary>
    Copy,

    /// <summary>Копировать H.265 с тегом hvc1 (браузер поддерживает HEVC).</summary>
    CopyHevc,

    /// <summary>Перекодировать в H.264.</summary>
    Transcode,
}

/// <summary>Подготовка и остановка HLS-сессий воспроизведения архива.</summary>
public interface IArchivePlaybackManager
{
    Task<ArchivePlaybackSession> StartAsync(
        IReadOnlyList<PlaybackSourceFile> files,
        ArchiveVideoMode mode,
        CancellationToken cancellationToken);

    ArchivePlaybackFile? FindFile(Guid sessionId, string fileName);

    void Stop(Guid sessionId);
}
