using System.Globalization;
using Microsoft.Extensions.Logging;
using MollyNvr.Application.Cameras;
using MollyNvr.Application.Recording;

namespace MollyNvr.Infrastructure.Recording;

/// <summary>Indexes finished segments and deletes those outside the schedule or events.</summary>
public sealed class SegmentIndexer(
    IRecordingSegmentStore segmentStore,
    IRecordingEventStore eventStore,
    RecordingOptions options,
    ILogger<SegmentIndexer> logger)
{
    private const string FilePrefix = "seg-";
    private const string FileSuffix = ".mp4";

    public async Task RunAsync(
        StorageSettings storage,
        IReadOnlyList<RecordingSettings> settingsList,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storage.Path))
        {
            return;
        }

        var now = DateTimeOffset.Now;
        var known = (await segmentStore.ListAsync(cancellationToken))
            .ToDictionary(static segment => segment.Path, StringComparer.Ordinal);

        if (!Directory.Exists(storage.Path))
        {
                        // The pool directory is gone (volume detached) — clear the index so the quota is not wrong.
            await RemoveMissingAsync(storage.Path, known.Values, cancellationToken);
            return;
        }

        var settingsByCamera = settingsList.ToDictionary(static settings => settings.CameraId);

        var events = await eventStore.BetweenAsync(now.AddDays(-3), now, cancellationToken);
        var eventsByCamera = events
            .GroupBy(static cameraEvent => cameraEvent.CameraId)
            .ToDictionary(static group => group.Key, static group => group.ToList());

        var deletedAny = false;

        foreach (var file in Directory.EnumerateFiles(storage.Path, "*" + FileSuffix, SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (known.ContainsKey(file))
            {
                continue;
            }

            FileInfo info;

            try
            {
                info = new FileInfo(file);

                if (!info.Exists || info.Length == 0)
                {
                    continue;
                }
            }
            catch (IOException)
            {
                continue;
            }

            var end = new DateTimeOffset(info.LastWriteTime);

                        // The file is still being written: ffmpeg closes a segment only after segment_time.
            if (now - end < TimeSpan.FromSeconds(options.SegmentSeconds + 15))
            {
                continue;
            }

            var cameraId = CameraIdFromPath(storage.Path, file);

            if (cameraId is null ||
                !settingsByCamera.TryGetValue(cameraId.Value, out var settings) ||
                !settings.Enabled)
            {
                TryDelete(info, ref deletedAny);
                continue;
            }

            var start = StartFromName(info.Name) ?? end.AddSeconds(-options.SegmentSeconds);

            if (IsWanted(settings, eventsByCamera.GetValueOrDefault(cameraId.Value), start, end))
            {
                await segmentStore.AddAsync(
                    new RecordingSegment(
                        Guid.NewGuid(),
                        cameraId.Value,
                        file,
                        start,
                        end,
                        info.Length),
                    cancellationToken);

                known[file] = new RecordingSegment(Guid.Empty, cameraId.Value, file, start, end, info.Length);
                continue;
            }

                        // The segment may be needed as pre-roll for an event that is about to start.
            if (now - end > TimeSpan.FromSeconds(settings.PreRollSeconds + 120))
            {
                TryDelete(info, ref deletedAny);
            }
        }

        await RemoveMissingAsync(storage.Path, known.Values, cancellationToken);

        if (deletedAny)
        {
            RemoveEmptyDirectories(storage.Path);
        }
    }

    private static bool IsWanted(
        RecordingSettings settings,
        IReadOnlyList<RecordingEvent>? events,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        if (settings.ContinuousEnabled && settings.ContinuousSchedule.Overlaps(start, end))
        {
            return true;
        }

        if (!settings.EventEnabled || events is null)
        {
            return false;
        }

        foreach (var cameraEvent in events)
        {
            var windowStart = cameraEvent.StartedAt.AddSeconds(-settings.PreRollSeconds);
            var windowEnd = (cameraEvent.EndedAt ?? DateTimeOffset.Now).AddSeconds(settings.PostRollSeconds);

            if (start <= windowEnd && end >= windowStart)
            {
                return true;
            }
        }

        return false;
    }

    private async Task RemoveMissingAsync(
        string poolPath,
        IEnumerable<RecordingSegment> segments,
        CancellationToken cancellationToken)
    {
        var missing = segments
            .Where(segment => segment.Path.StartsWith(poolPath, StringComparison.OrdinalIgnoreCase))
            .Where(segment => !File.Exists(segment.Path))
            .Select(static segment => segment.Id)
            .Where(static id => id != Guid.Empty)
            .ToList();

        if (missing.Count > 0)
        {
            await segmentStore.RemoveAsync(missing, cancellationToken);
        }
    }

    private static Guid? CameraIdFromPath(string poolPath, string file)
    {
        var relative = Path.GetRelativePath(poolPath, file);
        var separator = relative.IndexOf(Path.DirectorySeparatorChar);

        if (separator <= 0)
        {
            return null;
        }

        return Guid.TryParseExact(relative[..separator], "N", out var cameraId) ? cameraId : null;
    }

    private static DateTimeOffset? StartFromName(string name)
    {
        if (!name.StartsWith(FilePrefix, StringComparison.Ordinal) ||
            !name.EndsWith(FileSuffix, StringComparison.Ordinal))
        {
            return null;
        }

        var value = name[FilePrefix.Length..^FileSuffix.Length];

        return DateTime.TryParseExact(
            value,
            "yyyyMMdd-HHmmss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var parsed)
            ? new DateTimeOffset(parsed)
            : null;
    }

    private void TryDelete(FileInfo info, ref bool deletedAny)
    {
        try
        {
            info.Delete();
            deletedAny = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
                        logger.LogWarning(exception, "Failed to delete segment {Path}", info.FullName);
        }
    }

    private static void RemoveEmptyDirectories(string poolPath)
    {
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(poolPath, "*", SearchOption.AllDirectories)
                         .OrderByDescending(static path => path.Length))
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
                        // not critical: empty directories are removed next time
        }
    }
}

/// <summary>Pool overwrite: FIFO or watermarks.</summary>
public sealed class StorageRetention(
    IRecordingSegmentStore segmentStore,
    IStorageProbe storageProbe,
    ILogger<StorageRetention> logger)
{
    private const int BatchSize = 100;

    public async Task RunAsync(StorageSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Path) || !Directory.Exists(settings.Path))
        {
            return;
        }

        var (totalBytes, _) = storageProbe.GetVolumeSpace(settings.Path);
        var quota = totalBytes * settings.QuotaPercent / 100;

        if (quota <= 0)
        {
            return;
        }

        var used = await segmentStore.TotalSizeAsync(cancellationToken);
        var high = quota * settings.HighWatermarkPercent / 100;
        var low = quota * settings.LowWatermarkPercent / 100;

        var exceeded = settings.Mode == StorageOverwriteMode.Watermark
            ? used >= high
            : used > quota;

        if (!exceeded)
        {
            return;
        }

        var target = settings.Mode == StorageOverwriteMode.Watermark ? low : quota;

        while (used > target)
        {
            var batch = await segmentStore.OldestAsync(BatchSize, cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            var removed = new List<Guid>(batch.Count);

            foreach (var segment in batch)
            {
                try
                {
                    if (File.Exists(segment.Path))
                    {
                        File.Delete(segment.Path);
                    }

                    removed.Add(segment.Id);
                    used -= segment.SizeBytes;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                                        logger.LogWarning(exception, "Failed to delete the old segment {Path}", segment.Path);
                }
            }

            await segmentStore.RemoveAsync(removed, cancellationToken);

            if (removed.Count == 0)
            {
                break;
            }
        }
    }
}
