using System.Net;

namespace MollyNvr.Application.Discovery;

/// <summary>Устройство, найденное multicast-протоколом.</summary>
/// <param name="Address">IPv4-адрес устройства.</param>
/// <param name="Mac">MAC-адрес, если протокол его вернул.</param>
/// <param name="VendorKey">Ключ модуля производителя, если он известен.</param>
/// <param name="Vendor">Название производителя, если оно известно.</param>
/// <param name="Model">Модель устройства, если она известна.</param>
public sealed record DiscoveryResponse(
    IPAddress Address,
    string? Mac = null,
    string? VendorKey = null,
    string? Vendor = null,
    string? Model = null);

/// <summary>Multicast-протокол поиска камер в локальной сети.</summary>
public interface IDiscoveryProtocol
{
    /// <summary>Отображаемое имя протокола.</summary>
    string Name { get; }

    /// <summary>Ищет устройства; ошибки сети пробрасываются вызывающему коду.</summary>
    Task<IReadOnlyList<DiscoveryResponse>> DiscoverAsync(CancellationToken cancellationToken);
}
