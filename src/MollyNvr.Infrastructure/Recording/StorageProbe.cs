using System.Collections.Concurrent;
using MollyNvr.Application.Recording;

namespace MollyNvr.Infrastructure.Recording;

/// <summary>Проверка каталога и объёма тома для пула записей.</summary>
public sealed class StorageProbe : IStorageProbe
{
    public bool EnsureWritable(string path, out string? error)
    {
        error = null;

        try
        {
            Directory.CreateDirectory(path);
            var probe = Path.Combine(path, $".write-test-{Guid.NewGuid():N}");

            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            error = $"Каталог «{path}» недоступен для записи: {exception.Message}";
            return false;
        }
    }

    public (long TotalBytes, long FreeBytes) GetVolumeSpace(string path)
    {
        try
        {
            var drive = FindVolume(path);

            return drive is null
                ? (0L, 0L)
                : (drive.TotalSize, drive.AvailableFreeSpace);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (0L, 0L);
        }
    }

    /// <summary>Ищет точку монтирования с самым длинным совпадением пути.</summary>
    private static DriveInfo? FindVolume(string path)
    {
        var fullPath = Path.GetFullPath(path);
        DriveInfo? best = null;

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady)
            {
                continue;
            }

            var root = drive.RootDirectory.FullName;

            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (best is null || root.Length > best.RootDirectory.FullName.Length)
            {
                best = drive;
            }
        }

        return best;
    }
}

/// <summary>Текущее состояние процессов записи по камерам.</summary>
public sealed class RecordingStatusRegistry : IRecordingStatusRegistry
{
    private readonly ConcurrentDictionary<Guid, (bool Recording, DateTimeOffset? StartedAt, string? Error)> _state = new();

    public bool IsRecording(Guid cameraId) =>
        _state.TryGetValue(cameraId, out var state) && state.Recording;

    public DateTimeOffset? StartedAt(Guid cameraId) =>
        _state.TryGetValue(cameraId, out var state) ? state.StartedAt : null;

    public string? LastError(Guid cameraId) =>
        _state.TryGetValue(cameraId, out var state) ? state.Error : null;

    public void SetRecording(Guid cameraId, bool recording, string? error = null)
    {
        DateTimeOffset? startedAt = null;

        if (recording)
        {
            startedAt = _state.TryGetValue(cameraId, out var previous) && previous.Recording
                ? previous.StartedAt ?? DateTimeOffset.Now
                : DateTimeOffset.Now;
        }

        _state[cameraId] = (recording, startedAt, error);
    }
}
