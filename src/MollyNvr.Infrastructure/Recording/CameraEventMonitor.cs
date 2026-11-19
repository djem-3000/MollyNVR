using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MollyNvr.Application.Cameras;
using MollyNvr.Application.Recording;
using MollyNvr.Cameras.Abstractions;
using MollyNvr.Domain.Cameras;

namespace MollyNvr.Infrastructure.Recording;

/// <summary>
/// Subscribes to camera events (ONVIF PullPoint, ISAPI alert stream) and stores
/// event windows in the database — they drive event recording with pre/post-roll.
/// </summary>
public sealed class CameraEventMonitor(
    IRecordingSettingsStore settingsStore,
    IRecordingEventStore eventStore,
    ICameraRepository cameraRepository,
    CameraProbeService probeService,
    ILogger<CameraEventMonitor> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<Guid, Subscription> _subscriptions = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _openEvents = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncAsync(stoppingToken);
                await CloseStaleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                                logger.LogError(exception, "Camera event monitoring error");
            }

            try
            {
                await Task.Delay(Tick, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        foreach (var subscription in _subscriptions.Values)
        {
            subscription.Cancellation.Cancel();
        }
    }

    private async Task SyncAsync(CancellationToken cancellationToken)
    {
        var cameras = await cameraRepository.ListAsync(cancellationToken);
        var settings = await settingsStore.ListAsync(cancellationToken);
        var wanted = new Dictionary<Guid, (Camera Camera, RecordingSettings Settings, string Signature)>();

        foreach (var cameraSettings in settings)
        {
            if (!cameraSettings.Enabled || !cameraSettings.EventEnabled || cameraSettings.EventTypes.Count == 0)
            {
                continue;
            }

            var camera = cameras.FirstOrDefault(candidate => candidate.Id == cameraSettings.CameraId);
            if (camera is null)
            {
                continue;
            }

            var signature = string.Join(',', cameraSettings.EventTypes) + "|" + camera.Host;
            wanted[camera.Id] = (camera, cameraSettings, signature);
        }

        foreach (var (cameraId, subscription) in _subscriptions)
        {
            if (!wanted.TryGetValue(cameraId, out var current) || current.Signature != subscription.Signature)
            {
                subscription.Cancellation.Cancel();
                _subscriptions.TryRemove(cameraId, out _);
            }
        }

        foreach (var (cameraId, item) in wanted)
        {
            if (_subscriptions.ContainsKey(cameraId))
            {
                continue;
            }

            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _subscriptions[cameraId] = new Subscription(item.Signature, cancellation);

            _ = Task.Run(
                () => RunAsync(item.Camera, item.Settings, cancellation.Token),
                CancellationToken.None);
        }
    }

    private async Task RunAsync(Camera camera, RecordingSettings settings, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var credentials = await cameraRepository.GetCredentialsAsync(camera.Id, cancellationToken);
                if (credentials is null)
                {
                    return;
                }

                var endpoint = new CameraEndpoint(IPAddress.Parse(camera.Host), camera.RtspPort);
                var target = new CameraTarget(endpoint, credentials, camera.VendorKey);

                await foreach (var cameraEvent in probeService.WatchEventsAsync(target, cancellationToken))
                {
                    await HandleAsync(camera.Id, settings, cameraEvent, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                                        "Event subscription of camera '{Camera}' was interrupted, reconnecting in 15 s",
                    camera.Name);

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private async Task HandleAsync(
        Guid cameraId,
        RecordingSettings settings,
        CameraEvent cameraEvent,
        CancellationToken cancellationToken)
    {
        if (!settings.EventTypes.Contains(cameraEvent.Type, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        var key = Key(cameraId, cameraEvent.Type);

        if (cameraEvent.Active)
        {
            if (_openEvents.TryAdd(key, cameraEvent.Timestamp))
            {
                await eventStore.AddAsync(
                    new RecordingEvent(Guid.NewGuid(), cameraId, cameraEvent.Type, cameraEvent.Timestamp, null),
                    cancellationToken);
            }
            else
            {
                _openEvents[key] = cameraEvent.Timestamp;
            }

            return;
        }

        if (_openEvents.TryRemove(key, out _))
        {
            await eventStore.CloseAsync(cameraId, cameraEvent.Type, cameraEvent.Timestamp, cancellationToken);
        }
    }

    private async Task CloseStaleAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.Now;

        foreach (var (key, lastSeen) in _openEvents)
        {
            if (now - lastSeen < StaleAfter)
            {
                continue;
            }

            _openEvents.TryRemove(key, out _);
            var separator = key.IndexOf('|', StringComparison.Ordinal);

            if (separator <= 0 || !Guid.TryParseExact(key[..separator], "N", out var cameraId))
            {
                continue;
            }

            await eventStore.CloseAsync(cameraId, key[(separator + 1)..], lastSeen, cancellationToken);
        }
    }

    private static string Key(Guid cameraId, string type) => $"{cameraId:N}|{type}";

    private sealed record Subscription(string Signature, CancellationTokenSource Cancellation);
}
