using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MollyNvr.Application.Recording;
using MollyNvr.Infrastructure.Persistence;

namespace MollyNvr.Infrastructure.Tests;

public class ArchiveStoreTests
{
    [Fact]
    public async Task SegmentsAndEvents_AreFilteredByIntervalAndKeepUtcInstants()
    {
        using var directory = new TempDirectory();
        var storage = TestSupport.CreateStorageOptions(directory.Path);
        var services = new ServiceCollection();
        services.AddDbContextFactory<MollyNvrDbContext>(options =>
            options.UseSqlite($"Data Source={storage.DatabasePath}"));

        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<MollyNvrDbContext>>();

        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Database.MigrateAsync();
        }

        var segmentStore = new RecordingSegmentStore(factory);
        var eventStore = new RecordingEventStore(factory);
        var archiveStore = new ArchiveStore(factory);

        var cameraId = Guid.NewGuid();
        var offset = TimeSpan.FromHours(3);
        var start = new DateTimeOffset(2026, 11, 9, 21, 0, 0, offset);
        var second = start.AddMinutes(1);
        var dayLater = start.AddDays(1);
        var token = CancellationToken.None;

        await segmentStore.AddAsync(
            new RecordingSegment(Guid.NewGuid(), cameraId, "/tmp/a.mp4", start, second, 100),
            token);
        await segmentStore.AddAsync(
            new RecordingSegment(Guid.NewGuid(), cameraId, "/tmp/b.mp4", second, start.AddMinutes(2), 200),
            token);
        await eventStore.AddAsync(
            new RecordingEvent(Guid.NewGuid(), cameraId, "motion", start.AddSeconds(20), start.AddSeconds(50)),
            token);

        var segments = await archiveStore.SegmentsAsync(cameraId, start.AddHours(-1), dayLater, token);

        Assert.Equal(2, segments.Count);
        Assert.Equal(start.ToUniversalTime(), segments[0].StartedAt);
        Assert.Equal(second.ToUniversalTime(), segments[0].EndedAt);
        Assert.Equal(300, segments.Sum(static segment => segment.SizeBytes));

        var outside = await archiveStore.SegmentsAsync(cameraId, dayLater.AddHours(1), dayLater.AddHours(2), token);
        Assert.Empty(outside);

        var events = await eventStore.BetweenAsync(start, start.AddMinutes(5), token);
        Assert.Single(events);

        var archiveEvents = await archiveStore.EventsAsync(cameraId, start, dayLater, token);
        Assert.Single(archiveEvents);
        Assert.Equal("motion", archiveEvents[0].Type);
        Assert.Equal(start.AddSeconds(20).ToUniversalTime(), archiveEvents[0].StartedAt);

        var cameras = await archiveStore.CameraIdsWithRecordingsAsync(start, dayLater, token);
        Assert.Contains(cameraId, cameras);
    }
}
