using Microsoft.EntityFrameworkCore;
using MollyNvr.Application.Recording;

namespace MollyNvr.Infrastructure.Persistence;

/// <summary>SQLite-хранилище настроек записи камер.</summary>
public sealed class RecordingSettingsStore(IDbContextFactory<MollyNvrDbContext> factory) : IRecordingSettingsStore
{
    public async Task<RecordingSettings?> FindAsync(Guid cameraId, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var record = await db.RecordingSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(settings => settings.CameraId == cameraId, cancellationToken);

        return record is null ? null : Map(record);
    }

    public async Task<IReadOnlyList<RecordingSettings>> ListAsync(CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var records = await db.RecordingSettings.AsNoTracking().ToListAsync(cancellationToken);
        return records.Select(Map).ToList();
    }

    public async Task SaveAsync(RecordingSettings settings, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var record = await db.RecordingSettings
            .FirstOrDefaultAsync(candidate => candidate.CameraId == settings.CameraId, cancellationToken);

        if (record is null)
        {
            record = new RecordingSettingsRecord { CameraId = settings.CameraId };
            db.RecordingSettings.Add(record);
        }

        record.Enabled = settings.Enabled;
        record.ProfileId = settings.ProfileId;
        record.ContinuousEnabled = settings.ContinuousEnabled;
        record.ContinuousSchedule = settings.ContinuousSchedule.Serialize();
        record.EventEnabled = settings.EventEnabled;
        record.EventSchedule = settings.EventSchedule.Serialize();
        record.EventTypes = string.Join(',', settings.EventTypes);
        record.PreRollSeconds = settings.PreRollSeconds;
        record.PostRollSeconds = settings.PostRollSeconds;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid cameraId, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.RecordingSettings
            .Where(settings => settings.CameraId == cameraId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static RecordingSettings Map(RecordingSettingsRecord record) => new()
    {
        CameraId = record.CameraId,
        Enabled = record.Enabled,
        ProfileId = record.ProfileId,
        ContinuousEnabled = record.ContinuousEnabled,
        ContinuousSchedule = WeeklySchedule.Parse(record.ContinuousSchedule),
        EventEnabled = record.EventEnabled,
        EventSchedule = WeeklySchedule.Parse(record.EventSchedule),
        EventTypes = string.IsNullOrWhiteSpace(record.EventTypes)
            ? []
            : record.EventTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        PreRollSeconds = record.PreRollSeconds,
        PostRollSeconds = record.PostRollSeconds,
    };
}

/// <summary>SQLite-хранилище настроек пула (одна строка).</summary>
public sealed class StorageSettingsStore(IDbContextFactory<MollyNvrDbContext> factory) : IStorageSettingsStore
{
    private const int SingletonId = 1;

    public async Task<StorageSettings> GetAsync(CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var record = await db.StorageSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(settings => settings.Id == SingletonId, cancellationToken);

        return record is null
            ? new StorageSettings()
            : new StorageSettings
            {
                Path = record.Path,
                QuotaPercent = record.QuotaPercent,
                Mode = (StorageOverwriteMode)record.Mode,
                HighWatermarkPercent = record.HighWatermarkPercent,
                LowWatermarkPercent = record.LowWatermarkPercent,
            };
    }

    public async Task SaveAsync(StorageSettings settings, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var record = await db.StorageSettings
            .FirstOrDefaultAsync(candidate => candidate.Id == SingletonId, cancellationToken);

        if (record is null)
        {
            record = new StorageSettingsRecord { Id = SingletonId };
            db.StorageSettings.Add(record);
        }

        record.Path = settings.Path;
        record.QuotaPercent = settings.QuotaPercent;
        record.Mode = (int)settings.Mode;
        record.HighWatermarkPercent = settings.HighWatermarkPercent;
        record.LowWatermarkPercent = settings.LowWatermarkPercent;

        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>SQLite-индекс сегментов записи.</summary>
public sealed class RecordingSegmentStore(IDbContextFactory<MollyNvrDbContext> factory) : IRecordingSegmentStore
{
    public async Task AddAsync(RecordingSegment segment, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.RecordingSegments.Add(new RecordingSegmentRecord
        {
            Id = segment.Id,
            CameraId = segment.CameraId,
            Path = segment.Path,
            StartedAt = RecordingTime.ToUtc(segment.StartedAt),
            EndedAt = RecordingTime.ToUtc(segment.EndedAt),
            SizeBytes = segment.SizeBytes,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.RecordingSegments.AnyAsync(segment => segment.Path == path, cancellationToken);
    }

    public async Task<long> TotalSizeAsync(CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.RecordingSegments.SumAsync(segment => segment.SizeBytes, cancellationToken);
    }

    public async Task<IReadOnlyList<RecordingSegment>> OldestAsync(int limit, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var records = await db.RecordingSegments
            .AsNoTracking()
            .OrderBy(static segment => segment.StartedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return records.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<RecordingSegment>> ListAsync(CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var records = await db.RecordingSegments.AsNoTracking().ToListAsync(cancellationToken);
        return records.Select(Map).ToList();
    }

    public async Task<RecordingSegment?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var record = await db.RecordingSegments
            .AsNoTracking()
            .FirstOrDefaultAsync(segment => segment.Id == id, cancellationToken);

        return record is null ? null : Map(record);
    }

    public async Task RemoveAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return;
        }

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.RecordingSegments
            .Where(segment => ids.Contains(segment.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static RecordingSegment Map(RecordingSegmentRecord record) => new(
        record.Id,
        record.CameraId,
        record.Path,
        RecordingTime.ToOffset(record.StartedAt),
        RecordingTime.ToOffset(record.EndedAt),
        record.SizeBytes);
}

/// <summary>SQLite-окна событий камер.</summary>
public sealed class RecordingEventStore(IDbContextFactory<MollyNvrDbContext> factory) : IRecordingEventStore
{
    public async Task AddAsync(RecordingEvent recordingEvent, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.RecordingEvents.Add(new RecordingEventRecord
        {
            Id = recordingEvent.Id,
            CameraId = recordingEvent.CameraId,
            Type = recordingEvent.Type,
            StartedAt = RecordingTime.ToUtc(recordingEvent.StartedAt),
            EndedAt = RecordingTime.ToUtc(recordingEvent.EndedAt),
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CloseAsync(
        Guid cameraId,
        string type,
        DateTimeOffset endedAt,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.RecordingEvents
            .Where(recordingEvent =>
                recordingEvent.CameraId == cameraId &&
                recordingEvent.Type == type &&
                recordingEvent.EndedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    recordingEvent => recordingEvent.EndedAt,
                    RecordingTime.ToUtc(endedAt)),
                cancellationToken);
    }

    public async Task<IReadOnlyList<RecordingEvent>> BetweenAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var fromUtc = RecordingTime.ToUtc(from);
        var toUtc = RecordingTime.ToUtc(to);
        var records = await db.RecordingEvents
            .AsNoTracking()
            .Where(recordingEvent =>
                recordingEvent.StartedAt <= toUtc &&
                (recordingEvent.EndedAt == null || recordingEvent.EndedAt >= fromUtc))
            .ToListAsync(cancellationToken);

        return records.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<RecordingEvent>> ActiveAsync(CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var records = await db.RecordingEvents
            .AsNoTracking()
            .Where(recordingEvent => recordingEvent.EndedAt == null)
            .ToListAsync(cancellationToken);

        return records.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<RecordingEvent>> ListForCameraAsync(
        Guid cameraId,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var records = await db.RecordingEvents
            .AsNoTracking()
            .Where(recordingEvent => recordingEvent.CameraId == cameraId)
            .OrderByDescending(recordingEvent => recordingEvent.StartedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        return records.Select(Map).ToList();
    }

    private static RecordingEvent Map(RecordingEventRecord record) => new(
        record.Id,
        record.CameraId,
        record.Type,
        RecordingTime.ToOffset(record.StartedAt),
        RecordingTime.ToOffset(record.EndedAt));
}

/// <summary>Перевод времени между доменом (со смещением) и БД (UTC).</summary>
internal static class RecordingTime
{
    public static DateTime ToUtc(DateTimeOffset value) => value.UtcDateTime;

    public static DateTime? ToUtc(DateTimeOffset? value) => value?.UtcDateTime;

    public static DateTimeOffset ToOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public static DateTimeOffset? ToOffset(DateTime? value) =>
        value is null
            ? null
            : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
}
