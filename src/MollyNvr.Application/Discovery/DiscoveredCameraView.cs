namespace MollyNvr.Application.Discovery;

/// <summary>Device found during camera discovery.</summary>
/// <param name="Host">IPv4 address.</param>
/// <param name="Mac">MAC address, when it could be obtained.</param>
/// <param name="VendorKey">Vendor module key, when known.</param>
/// <param name="Vendor">Vendor name, when known.</param>
/// <param name="Model">Device model, when known.</param>
/// <param name="OpenPorts">Open typical device ports.</param>
public sealed record DiscoveredCameraView(
    string Host,
    string? Mac,
    string? VendorKey,
    string? Vendor,
    string? Model,
    IReadOnlyList<int> OpenPorts);
