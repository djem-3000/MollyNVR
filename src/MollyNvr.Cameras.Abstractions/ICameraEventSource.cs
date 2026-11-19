namespace MollyNvr.Cameras.Abstractions;

/// <summary>Event type a camera module can report (motion, person, vehicle, ...).</summary>
public sealed record CameraEventType(string Key, string Label);

/// <summary>Camera event: type, moment and start/end flag.</summary>
public sealed record CameraEvent(string Type, DateTimeOffset Timestamp, bool Active);

/// <summary>Camera event source (ONVIF PullPoint, ISAPI alert stream, etc.).</summary>
public interface ICameraEventSource
{
        /// <summary>Event types reported by the camera. An empty list means the source is unavailable.</summary>
    Task<IReadOnlyList<CameraEventType>> GetEventTypesAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CancellationToken cancellationToken);

        /// <summary>Camera event stream; must complete when the token is cancelled.</summary>
    IAsyncEnumerable<CameraEvent> WatchEventsAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CancellationToken cancellationToken);
}
