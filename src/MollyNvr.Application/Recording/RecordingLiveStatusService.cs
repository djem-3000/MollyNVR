namespace MollyNvr.Application.Recording;

/// <summary>Состояние записи камеры для индикатора REC в UI.</summary>
public sealed record RecordingLiveStatus(
    Guid CameraId,
    DateTimeOffset? StartedAt,
    bool Schedule,
    IReadOnlyList<string> Events);

/// <summary>
/// Собирает активные записи: режим расписания и открытые события камер.
/// </summary>
public sealed class RecordingLiveStatusService(
    IRecordingSettingsStore settingsStore,
    IRecordingEventStore eventStore,
    IRecordingStatusRegistry statusRegistry)
{
    public async Task<IReadOnlyList<RecordingLiveStatus>> GetAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.Now;
        var settings = await settingsStore.ListAsync(cancellationToken);
        var activeEvents = await eventStore.ActiveAsync(cancellationToken);

        var eventsByCamera = activeEvents
            .GroupBy(static item => item.CameraId)
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .Select(static item => item.Type)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList());

        var result = new List<RecordingLiveStatus>();

        foreach (var cameraSettings in settings)
        {
            if (!statusRegistry.IsRecording(cameraSettings.CameraId))
            {
                continue;
            }

            var schedule =
                (cameraSettings.ContinuousEnabled && cameraSettings.ContinuousSchedule.IsActive(now)) ||
                (cameraSettings.EventEnabled && cameraSettings.EventSchedule.IsActive(now));

            var events = cameraSettings.EventEnabled && cameraSettings.EventSchedule.IsActive(now) &&
                eventsByCamera.TryGetValue(cameraSettings.CameraId, out var open)
                    ? open
                        .Where(type => cameraSettings.EventTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
                        .ToList()
                    : [];

            result.Add(new RecordingLiveStatus(
                cameraSettings.CameraId,
                statusRegistry.StartedAt(cameraSettings.CameraId),
                schedule,
                events));
        }

        return result;
    }
}
