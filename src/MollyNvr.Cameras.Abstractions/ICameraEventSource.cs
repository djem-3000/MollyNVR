namespace MollyNvr.Cameras.Abstractions;

/// <summary>Тип события, который умеет отдавать модуль камеры (движение, человек, машина…).</summary>
public sealed record CameraEventType(string Key, string Label);

/// <summary>Событие камеры: тип, момент и признак начала/окончания.</summary>
public sealed record CameraEvent(string Type, DateTimeOffset Timestamp, bool Active);

/// <summary>Источник событий камеры (ONVIF PullPoint, ISAPI alert stream и т.п.).</summary>
public interface ICameraEventSource
{
    /// <summary>Типы событий, которые камера сообщает. Пустой список — источник недоступен.</summary>
    Task<IReadOnlyList<CameraEventType>> GetEventTypesAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CancellationToken cancellationToken);

    /// <summary>Поток событий камеры; должен завершаться при отмене токена.</summary>
    IAsyncEnumerable<CameraEvent> WatchEventsAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CancellationToken cancellationToken);
}
