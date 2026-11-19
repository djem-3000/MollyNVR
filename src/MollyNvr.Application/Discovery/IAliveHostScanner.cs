using System.Net;

namespace MollyNvr.Application.Discovery;

/// <summary>
/// Fast check of which addresses in a range respond at all (ICMP echo).
/// Needed to scan ports only on live hosts: a full port sweep
/// across a /24 creates a connection burst that Docker Desktop NAT drops.
/// </summary>
public interface IAliveHostScanner
{
    /// <summary>
        /// Returns the responding addresses; <c>null</c> means the check is unavailable on this platform
        /// or without privileges, and the caller scans the whole range.
    /// </summary>
    Task<IReadOnlySet<IPAddress>?> FindAliveHostsAsync(
        IReadOnlyList<IPAddress> hosts,
        CancellationToken cancellationToken);
}
