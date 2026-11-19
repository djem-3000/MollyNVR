using MollyNvr.Application.Cameras;
using MollyNvr.Cameras.Abstractions;
using MollyNvr.Domain.Cameras;

namespace MollyNvr.Application.Recording;

/// <summary>Camera recording settings (request from the UI).</summary>
public sealed record RecordingSettingsRequest(
    bool Enabled,
    string? ProfileId,
    bool ContinuousEnabled,
    string? ContinuousSchedule,
    bool EventEnabled,
    string? EventSchedule,
    IReadOnlyList<string>? EventTypes,
    int PreRollSeconds,
    int PostRollSeconds);

/// <summary>Pool settings (request from the UI).</summary>
public sealed record StorageSettingsRequest(
    string? Path,
    int QuotaPercent,
    StorageOverwriteMode Mode,
    int HighWatermarkPercent,
    int LowWatermarkPercent);

/// <summary>Camera recording settings and state.</summary>
public sealed record RecordingView(
    Guid CameraId,
    bool Enabled,
    string ProfileId,
    bool ContinuousEnabled,
    string ContinuousSchedule,
    bool EventEnabled,
    string EventSchedule,
    IReadOnlyList<string> EventTypes,
    int PreRollSeconds,
    int PostRollSeconds,
    IReadOnlyList<CameraEventType> SupportedEventTypes,
    bool EventsSupported,
    bool IsRecording,
    string? LastError);

/// <summary>Recording pool and its utilization.</summary>
public sealed record StorageView(StorageSettings Settings, StoragePoolStatus Status);

/// <summary>Recording settings: reading, saving, pool and state.</summary>
public sealed class RecordingService(
    IRecordingSettingsStore settingsStore,
    IStorageSettingsStore storageStore,
    IRecordingSegmentStore segmentStore,
    IStorageProbe storageProbe,
    ICameraRepository cameraRepository,
    CameraProbeService probeService,
    IRecordingStatusRegistry statusRegistry,
    IRecordingSettingsNotifier settingsNotifier)
{
    private static readonly int[] AllowedRollSeconds = [0, 15, 30, 60];

    public async Task<RecordingView> GetAsync(Guid cameraId, CancellationToken cancellationToken)
    {
        var camera = await cameraRepository.FindAsync(cameraId, cancellationToken)
            ?? throw new CameraNotFoundException(cameraId);

        var settings = await settingsStore.FindAsync(cameraId, cancellationToken) ?? Defaults(camera);
        var supported = await GetSupportedEventsAsync(camera, cancellationToken);

        return ToView(settings, supported);
    }

    public async Task<RecordingView> SaveAsync(
        Guid cameraId,
        RecordingSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var camera = await cameraRepository.FindAsync(cameraId, cancellationToken)
            ?? throw new CameraNotFoundException(cameraId);

        var profileId = string.IsNullOrWhiteSpace(request.ProfileId)
            ? camera.MainProfileId
            : request.ProfileId.Trim();

        if (!camera.Profiles.Any(profile => string.Equals(profile.Id, profileId, StringComparison.Ordinal)))
        {
                        throw new ArgumentException($"Profile '{profileId}' was not found on the camera.", nameof(request));
        }

        var settings = new RecordingSettings
        {
            CameraId = cameraId,
            Enabled = request.Enabled,
            ProfileId = profileId,
            ContinuousEnabled = request.ContinuousEnabled,
            ContinuousSchedule = WeeklySchedule.Parse(request.ContinuousSchedule),
            EventEnabled = request.EventEnabled,
            EventSchedule = WeeklySchedule.Parse(request.EventSchedule),
            EventTypes = NormalizeEventTypes(request.EventTypes),
            PreRollSeconds = ClampRoll(request.PreRollSeconds),
            PostRollSeconds = ClampRoll(request.PostRollSeconds),
        };

        await settingsStore.SaveAsync(settings, cancellationToken);
        settingsNotifier.Notify();
        var supported = await GetSupportedEventsAsync(camera, cancellationToken);

        return ToView(settings, supported);
    }

    public async Task<StorageView> GetStorageAsync(CancellationToken cancellationToken)
    {
        var settings = await storageStore.GetAsync(cancellationToken);
        return new StorageView(settings, await BuildStatusAsync(settings, cancellationToken));
    }

    public async Task<StorageView> SaveStorageAsync(
        StorageSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var path = string.IsNullOrWhiteSpace(request.Path)
                        ? throw new ArgumentException("Specify a directory for recordings.", nameof(request))
            : Path.GetFullPath(request.Path.Trim());

        if (!storageProbe.EnsureWritable(path, out var error))
        {
                        throw new ArgumentException(error ?? "The directory is not writable.", nameof(request));
        }

        var quota = Math.Clamp(request.QuotaPercent, 10, 99);
        var high = Math.Clamp(request.HighWatermarkPercent, 10, 99);
        var low = Math.Clamp(request.LowWatermarkPercent, 5, high - 1);

        var settings = new StorageSettings
        {
            Path = path,
            QuotaPercent = quota,
            Mode = request.Mode,
            HighWatermarkPercent = high,
            LowWatermarkPercent = low,
        };

        await storageStore.SaveAsync(settings, cancellationToken);
        settingsNotifier.Notify();
        return new StorageView(settings, await BuildStatusAsync(settings, cancellationToken));
    }

        /// <summary>Current pool state (used by background cleanup and the UI).</summary>
    public async Task<StoragePoolStatus> BuildStatusAsync(
        StorageSettings settings,
        CancellationToken cancellationToken)
    {
        var used = await segmentStore.TotalSizeAsync(cancellationToken);
        var path = string.IsNullOrWhiteSpace(settings.Path) ? string.Empty : settings.Path;
        var (total, free) = string.IsNullOrEmpty(path) || !Directory.Exists(path)
            ? (0L, 0L)
            : storageProbe.GetVolumeSpace(path);

        var quota = total * settings.QuotaPercent / 100;

        return new StoragePoolStatus(path, total, free, quota, used);
    }

    private RecordingSettings Defaults(Camera camera) => new()
    {
        CameraId = camera.Id,
        ProfileId = camera.MainProfileId,
    };

    private RecordingView ToView(RecordingSettings settings, IReadOnlyList<CameraEventType> supported) =>
        new(
            CameraId: settings.CameraId,
            Enabled: settings.Enabled,
            ProfileId: settings.ProfileId,
            ContinuousEnabled: settings.ContinuousEnabled,
            ContinuousSchedule: settings.ContinuousSchedule.Serialize(),
            EventEnabled: settings.EventEnabled,
            EventSchedule: settings.EventSchedule.Serialize(),
            EventTypes: settings.EventTypes,
            PreRollSeconds: settings.PreRollSeconds,
            PostRollSeconds: settings.PostRollSeconds,
            SupportedEventTypes: supported,
            EventsSupported: supported.Count > 0,
            IsRecording: statusRegistry.IsRecording(settings.CameraId),
            LastError: statusRegistry.LastError(settings.CameraId));

    private async Task<IReadOnlyList<CameraEventType>> GetSupportedEventsAsync(
        Camera camera,
        CancellationToken cancellationToken)
    {
        var credentials = await cameraRepository.GetCredentialsAsync(camera.Id, cancellationToken);
        if (credentials is null)
        {
            return [];
        }

        var endpoint = new CameraEndpoint(System.Net.IPAddress.Parse(camera.Host), camera.RtspPort);
        var target = new CameraTarget(endpoint, credentials, camera.VendorKey);
        return await probeService.GetEventTypesAsync(target, cancellationToken);
    }

    private static IReadOnlyList<string> NormalizeEventTypes(IReadOnlyList<string>? types) =>
        types is null
            ? []
            : types
                .Where(static type => !string.IsNullOrWhiteSpace(type))
                .Select(static type => type.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static type => type, StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static int ClampRoll(int seconds) =>
        AllowedRollSeconds.Contains(seconds) ? seconds : 0;
}
