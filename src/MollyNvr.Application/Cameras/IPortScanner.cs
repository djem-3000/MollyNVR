using System.Net;

namespace MollyNvr.Application.Cameras;

/// <summary>Сканирование TCP-портов устройства при опросе камеры.</summary>
public interface IPortScanner
{
    /// <summary>Возвращает открытые порты из указанного набора.</summary>
    Task<IReadOnlyList<int>> FindOpenPortsAsync(
        IPAddress host,
        IReadOnlyList<int> ports,
        CancellationToken cancellationToken);
}
