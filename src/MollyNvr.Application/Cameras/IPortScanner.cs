using System.Net;

namespace MollyNvr.Application.Cameras;

/// <summary>TCP port scanning of a device during a camera probe.</summary>
public interface IPortScanner
{
        /// <summary>Returns open ports from the specified set.</summary>
    Task<IReadOnlyList<int>> FindOpenPortsAsync(
        IPAddress host,
        IReadOnlyList<int> ports,
        CancellationToken cancellationToken);
}
