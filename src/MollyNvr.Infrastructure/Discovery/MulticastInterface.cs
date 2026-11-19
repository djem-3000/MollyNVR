using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MollyNvr.Infrastructure.Discovery;

/// <summary>
/// Selects the local network IPv4 address for multicast discovery. VPN adapters (Wintun, WireGuard)
/// often capture the 224.0.0.0/4 route, so the interface is specified explicitly.
/// </summary>
internal static class MulticastInterface
{
    public static IPAddress? FindLanAddress()
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(IsUsable)
            .Select(CreateCandidate)
            .OfType<Candidate>()
            .OrderBy(static candidate => candidate.Score)
            .ToList();

        return candidates.Count == 0 ? null : candidates[0].Address;
    }

    private static bool IsUsable(NetworkInterface networkInterface) =>
        networkInterface.OperationalStatus == OperationalStatus.Up &&
        networkInterface.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel);

    private static Candidate? CreateCandidate(NetworkInterface networkInterface)
    {
        var properties = networkInterface.GetIPProperties();
        var address = properties.UnicastAddresses
            .Select(static unicast => unicast.Address)
            .FirstOrDefault(static unicast => unicast.AddressFamily == AddressFamily.InterNetwork);

        if (address is null)
        {
            return null;
        }

        var hasGateway = properties.GatewayAddresses.Any(static gateway =>
            gateway.Address.AddressFamily == AddressFamily.InterNetwork &&
            !gateway.Address.Equals(IPAddress.Any));

        var kindScore = networkInterface.NetworkInterfaceType is
            NetworkInterfaceType.Ethernet or
            NetworkInterfaceType.GigabitEthernet or
            NetworkInterfaceType.Wireless80211
                ? 0
                : 1;

        return new Candidate(address, (hasGateway ? 0 : 2) + kindScore);
    }

        /// <summary>Lower is better: interfaces with a gateway first, then wired and Wi-Fi.</summary>
    private sealed record Candidate(IPAddress Address, int Score);
}
