using Microsoft.EntityFrameworkCore;
using MollyNvr.Application.Archive;
using MollyNvr.Application.Recording;

namespace MollyNvr.Infrastructure.Persistence;

/// <summary>Reading recording segments and events for the archive.</summary>
public sealed class ArchiveStore(IDbContextFactory<MollyNvrDbContext> factory) : IArchiveStore
{
    public async Task<IReadOnlyList<RecordingSegment>> SegmentsAsync(
        Guid cameraId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var fromUtc = RecordingTime.ToUtc(from);
        var toUtc = RecordingTime.ToUtc(to);
        var records = await db.RecordingSegments
            .AsNoTracking()
            .Where(segment =>
                segment.CameraId == cameraId &&
                segment.StartedAt < toUtc &&
                segment.EndedAt > fromUtc)
            .OrderBy(static segment => segment.StartedAt)
            .ToListAsync(cancellationToken);

        return records
            .Select(static record => new RecordingSegment(
                record.Id,
                record.CameraId,
                record.Path,
                RecordingTime.ToOffset(record.StartedAt),
                RecordingTime.ToOffset(record.EndedAt),
                record.SizeBytes))
            .ToList();
    }

    public async Task<IReadOnlyList<RecordingEvent>> EventsAsync(
        Guid cameraId,
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
                recordingEvent.CameraId == cameraId &&
                recordingEvent.StartedAt < toUtc &&
                (recordingEvent.EndedAt == null || recordingEvent.EndedAt > fromUtc))
            .OrderBy(static recordingEvent => recordingEvent.StartedAt)
            .ToListAsync(cancellationToken);

        return records
            .Select(static record => new RecordingEvent(
                record.Id,
                record.CameraId,
                record.Type,
                RecordingTime.ToOffset(record.StartedAt),
                RecordingTime.ToOffset(record.EndedAt)))
            .ToList();
    }

    public async Task<IReadOnlyList<Guid>> CameraIdsWithRecordingsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var fromUtc = RecordingTime.ToUtc(from);
        var toUtc = RecordingTime.ToUtc(to);

        return await db.RecordingSegments
            .AsNoTracking()
            .Where(segment => segment.StartedAt < toUtc && segment.EndedAt > fromUtc)
            .Select(static segment => segment.CameraId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
