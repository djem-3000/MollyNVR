using System.Net;

namespace MollyNvr.Application.Discovery;

/// <summary>Host ARP table: IP -> MAC. May be incomplete inside a container.</summary>
public interface IArpTableResolver
{
        /// <summary>Returns known MAC addresses; an empty dictionary when the table is unavailable.</summary>
    Task<IReadOnlyDictionary<IPAddress, string>> GetMacAddressesAsync(
        CancellationToken cancellationToken);
}
