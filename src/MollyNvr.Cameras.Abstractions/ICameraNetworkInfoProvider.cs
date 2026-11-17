namespace MollyNvr.Cameras.Abstractions;

/// <summary>Дополнительные сетевые параметры, которые модуль может запросить у камеры.</summary>
public interface ICameraNetworkInfoProvider
{
    /// <summary>
    /// Возвращает состояние DHCP или <c>null</c>, если камера не сообщает этот параметр.
    /// </summary>
    Task<bool?> GetDhcpEnabledAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CancellationToken cancellationToken);
}
