using MollyNvr.Application.Recording;

namespace MollyNvr.Application.Tests;

public sealed class RecordingLiveStatusServiceTests
{
    [Fact]
    public async Task ReturnsOnlyCamerasThatAreRecording()
    {
        var cameraId = Guid.NewGuid();
        var registry = new FakeStatusRegistry();
        var service = CreateService(cameraId, Settings(cameraId), [], registry);

        var result = await service.GetAsync(CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ReportsScheduleAndOpenEvents()
    {
        var cameraId = Guid.NewGuid();
        var registry = new FakeStatusRegistry();
        registry.SetRecording(cameraId, true);

        var openEvents = new[]
        {
            new RecordingEvent(Guid.NewGuid(), cameraId, "person", DateTimeOffset.Now, null),
            new RecordingEvent(Guid.NewGuid(), cameraId, "vehicle", DateTimeOffset.Now, null),
        };

        var service = CreateService(cameraId, Settings(cameraId), openEvents, registry);
        var item = Assert.Single(await service.GetAsync(CancellationToken.None));

        Assert.True(item.Schedule);
        Assert.NotNull(item.StartedAt);
        Assert.Equal(["person", "vehicle"], item.Events);
    }

    [Fact]
    public async Task IgnoresEventTypesThatAreNotEnabledForCamera()
    {
        var cameraId = Guid.NewGuid();
        var registry = new FakeStatusRegistry();
        registry.SetRecording(cameraId, true);

        var settings = Settings(cameraId);
        settings = settings with { EventTypes = ["motion"] };

        var openEvents = new[]
        {
            new RecordingEvent(Guid.NewGuid(), cameraId, "person", DateTimeOffset.Now, null),
            new RecordingEvent(Guid.NewGuid(), cameraId, "motion", DateTimeOffset.Now, null),
        };

        var service = CreateService(cameraId, settings, openEvents, registry);
        var item = Assert.Single(await service.GetAsync(CancellationToken.None));

        Assert.Equal(["motion"], item.Events);
    }

    [Fact]
    public async Task DoesNotReportScheduleWhenContinuousRecordingIsOff()
    {
        var cameraId = Guid.NewGuid();
        var registry = new FakeStatusRegistry();
        registry.SetRecording(cameraId, true);

        var settings = Settings(cameraId) with
        {
            ContinuousEnabled = false,
            EventEnabled = false,
        };

        var service = CreateService(cameraId, settings, [], registry);
        var item = Assert.Single(await service.GetAsync(CancellationToken.None));

        Assert.False(item.Schedule);
        Assert.Empty(item.Events);
    }

    [Fact]
    public async Task ReportsScheduleWhenOnlyEventWindowIsActive()
    {
        var cameraId = Guid.NewGuid();
        var registry = new FakeStatusRegistry();
        registry.SetRecording(cameraId, true);

        var settings = Settings(cameraId) with { ContinuousEnabled = false };

        var service = CreateService(cameraId, settings, [], registry);
        var item = Assert.Single(await service.GetAsync(CancellationToken.None));

        Assert.True(item.Schedule);
        Assert.Empty(item.Events);
    }

    private static RecordingSettings Settings(Guid cameraId) => new()
    {
        CameraId = cameraId,
        Enabled = true,
        ContinuousEnabled = true,
        ContinuousSchedule = WeeklySchedule.Always,
        EventEnabled = true,
        EventSchedule = WeeklySchedule.Always,
        EventTypes = ["motion", "person", "vehicle"],
    };

    private static RecordingLiveStatusService CreateService(
        Guid cameraId,
        RecordingSettings settings,
        IReadOnlyList<RecordingEvent> events,
        IRecordingStatusRegistry registry) =>
        new(
            new FakeSettingsStore(cameraId, settings),
            new FakeEventStore(events),
            registry);

    private sealed class FakeSettingsStore(Guid cameraId, RecordingSettings settings) : IRecordingSettingsStore
    {
        public Task<RecordingSettings?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<RecordingSettings?>(id == cameraId ? settings : null);

        public Task<IReadOnlyList<RecordingSettings>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RecordingSettings>>([settings]);

        public Task SaveAsync(RecordingSettings value, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RemoveAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeEventStore(IReadOnlyList<RecordingEvent> events) : IRecordingEventStore
    {
        public Task AddAsync(RecordingEvent recordingEvent, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task CloseAsync(
            Guid cameraId,
            string type,
            DateTimeOffset endedAt,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<RecordingEvent>> BetweenAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RecordingEvent>>(events);

        public Task<IReadOnlyList<RecordingEvent>> ActiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RecordingEvent>>(events);

        public Task<IReadOnlyList<RecordingEvent>> ListForCameraAsync(
            Guid cameraId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RecordingEvent>>(events);
    }

    private sealed class FakeStatusRegistry : IRecordingStatusRegistry
    {
        private readonly Dictionary<Guid, (bool Recording, DateTimeOffset? StartedAt)> _state = [];

        public bool IsRecording(Guid cameraId) =>
            _state.TryGetValue(cameraId, out var state) && state.Recording;

        public DateTimeOffset? StartedAt(Guid cameraId) =>
            _state.TryGetValue(cameraId, out var state) ? state.StartedAt : null;

        public string? LastError(Guid cameraId) => null;

        public void SetRecording(Guid cameraId, bool recording, string? error = null) =>
            _state[cameraId] = (recording, recording ? DateTimeOffset.Now : null);
    }
}
