namespace MollyNvr.Application.Recording;

/// <summary>Storage of camera recording settings.</summary>
public interface IRecordingSettingsStore
{
    Task<RecordingSettings?> FindAsync(Guid cameraId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RecordingSettings>> ListAsync(CancellationToken cancellationToken);

    Task SaveAsync(RecordingSettings settings, CancellationToken cancellationToken);

    Task RemoveAsync(Guid cameraId, CancellationToken cancellationToken);
}

/// <summary>Storage of the shared recording pool settings (a single record).</summary>
public interface IStorageSettingsStore
{
    Task<StorageSettings> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(StorageSettings settings, CancellationToken cancellationToken);
}

/// <summary>Index of recording segments on disk.</summary>
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

/// <summary>Camera event windows (for pre/post-roll and cleanup of unbound segments).</summary>
public interface IRecordingEventStore
{
    Task AddAsync(RecordingEvent recordingEvent, CancellationToken cancellationToken);

    Task CloseAsync(Guid cameraId, string type, DateTimeOffset endedAt, CancellationToken cancellationToken);

        /// <summary>Events intersecting the interval (including open ones).</summary>
    Task<IReadOnlyList<RecordingEvent>> BetweenAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);

        /// <summary>Open event windows (without an end time).</summary>
    Task<IReadOnlyList<RecordingEvent>> ActiveAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<RecordingEvent>> ListForCameraAsync(Guid cameraId, CancellationToken cancellationToken);
}

/// <summary>Details of the pool volume and directory.</summary>
public interface IStorageProbe
{
        /// <summary>Checks that the directory exists or can be created and is writable.</summary>
    bool EnsureWritable(string path, out string? error);

        /// <summary>Volume capacity and free space for the directory.</summary>
    (long TotalBytes, long FreeBytes) GetVolumeSpace(string path);
}

/// <summary>Current state of the recording processes (filled by the background service).</summary>
public interface IRecordingStatusRegistry
{
    bool IsRecording(Guid cameraId);

        /// <summary>Start moment of the current recording process.</summary>
    DateTimeOffset? StartedAt(Guid cameraId);

    string? LastError(Guid cameraId);

    void SetRecording(Guid cameraId, bool recording, string? error = null);
}
