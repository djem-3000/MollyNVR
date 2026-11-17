namespace MollyNvr.Application.Discovery;

/// <summary>Устройство, найденное при поиске камер.</summary>
/// <param name="Host">IPv4-адрес.</param>
/// <param name="Mac">MAC-адрес, если удалось получить.</param>
/// <param name="VendorKey">Ключ модуля производителя, если он известен.</param>
/// <param name="Vendor">Производитель, если он известен.</param>
/// <param name="Model">Модель устройства, если она известна.</param>
/// <param name="OpenPorts">Открытые типовые порты устройства.</param>
public sealed record DiscoveredCameraView(
    string Host,
    string? Mac,
    string? VendorKey,
    string? Vendor,
    string? Model,
    IReadOnlyList<int> OpenPorts);
