namespace MollyNvr.Cameras.Abstractions;

/// <summary>Модуль производителя камер: опрос, профили потоков, снапшот.</summary>
public interface ICameraModule
{
    /// <summary>Ключ модуля (например, <c>onvif</c>).</summary>
    string Key { get; }

    /// <summary>Отображаемое имя производителя.</summary>
    string DisplayName { get; }

    /// <summary>
    /// Быстрая проверка, отвечает ли устройство на этом адресе по протоколу модуля
    /// (без учётных данных). Используется для автопоиска и перебора модулей.
    /// </summary>
    Task<bool> CanHandleAsync(CameraEndpoint endpoint, CancellationToken cancellationToken);

    /// <summary>Опрос устройства: сведения, профили и потоки.</summary>
    /// <exception cref="CameraAuthenticationException">Камера отклонила учётные данные.</exception>
    /// <exception cref="CameraUnavailableException">Устройство недоступно или не отвечает по протоколу.</exception>
    Task<CameraProbeResult> ProbeAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CancellationToken cancellationToken);

    /// <summary>JPEG-снапшот профиля камеры.</summary>
    Task<byte[]> GetSnapshotAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CameraProfile profile,
        CancellationToken cancellationToken);
}
