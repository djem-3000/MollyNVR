namespace MollyNvr.Application.Recording;

/// <summary>Хранилище настроек записи камер.</summary>
public interface IRecordingSettingsStore
{
    Task<RecordingSettings?> FindAsync(Guid cameraId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RecordingSettings>> ListAsync(CancellationToken cancellationToken);

    Task SaveAsync(RecordingSettings settings, CancellationToken cancellationToken);

    Task RemoveAsync(Guid cameraId, CancellationToken cancellationToken);
}

/// <summary>Хранилище настроек общего пула записей (одна запись).</summary>
public interface IStorageSettingsStore
{
    Task<StorageSettings> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(StorageSettings settings, CancellationToken cancellationToken);
}

/// <summary>Индекс сегментов записи на диске.</summary>
public interface IRecordingSegmentStore
{
    Task AddAsync(RecordingSegment segment, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken);

    Task<long> TotalSizeAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<RecordingSegment>> OldestAsync(int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<RecordingSegment>> ListAsync(CancellationToken cancellationToken);

    Task<RecordingSegment?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task RemoveAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}

/// <summary>Окна событий камеры (для pre/post-roll и очистки непривязанных сегментов).</summary>
public interface IRecordingEventStore
{
    Task AddAsync(RecordingEvent recordingEvent, CancellationToken cancellationToken);

    Task CloseAsync(Guid cameraId, string type, DateTimeOffset endedAt, CancellationToken cancellationToken);

    /// <summary>События, пересекающиеся с интервалом (с учётом открытых).</summary>
    Task<IReadOnlyList<RecordingEvent>> BetweenAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);

    /// <summary>Открытые окна событий (без времени окончания).</summary>
    Task<IReadOnlyList<RecordingEvent>> ActiveAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<RecordingEvent>> ListForCameraAsync(Guid cameraId, CancellationToken cancellationToken);
}

/// <summary>Сведения о томе и каталоге пула.</summary>
public interface IStorageProbe
{
    /// <summary>Проверяет, что каталог существует или создаётся и доступен для записи.</summary>
    bool EnsureWritable(string path, out string? error);

    /// <summary>Объём тома и свободное место для каталога.</summary>
    (long TotalBytes, long FreeBytes) GetVolumeSpace(string path);
}

/// <summary>Текущее состояние процессов записи (заполняет фоновый сервис).</summary>
public interface IRecordingStatusRegistry
{
    bool IsRecording(Guid cameraId);

    /// <summary>Момент запуска текущего процесса записи.</summary>
    DateTimeOffset? StartedAt(Guid cameraId);

    string? LastError(Guid cameraId);

    void SetRecording(Guid cameraId, bool recording, string? error = null);
}
