using System.Net;

namespace MollyNvr.Application.Discovery;

/// <summary>Device found by a multicast protocol.</summary>
/// <param name="Address">Device IPv4 address.</param>
/// <param name="Mac">MAC address, when the protocol returned it.</param>
/// <param name="VendorKey">Vendor module key, when known.</param>
/// <param name="Vendor">Vendor name, when known.</param>
/// <param name="Model">Device model, when known.</param>
public sealed record DiscoveryResponse(
    IPAddress Address,
    string? Mac = null,
    string? VendorKey = null,
    string? Vendor = null,
    string? Model = null);

/// <summary>Multicast protocol for finding cameras on the local network.</summary>
public interface IDiscoveryProtocol
{
        /// <summary>Display name of the protocol.</summary>
    string Name { get; }

        /// <summary>Finds devices; network errors are propagated to the caller.</summary>
    Task<IReadOnlyList<DiscoveryResponse>> DiscoverAsync(CancellationToken cancellationToken);
}
