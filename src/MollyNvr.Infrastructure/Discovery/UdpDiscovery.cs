using System.Net;
using System.Net.Sockets;

namespace MollyNvr.Infrastructure.Discovery;

/// <summary>Common UDP discovery operations: sending a request and collecting responses until the timeout.</summary>
internal static class UdpDiscovery
{
    public static async Task<IReadOnlyList<(byte[] Data, IPEndPoint Remote)>> QueryAsync(
        byte[] payload,
        IReadOnlyList<IPEndPoint> targets,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var client = new UdpClient(AddressFamily.InterNetwork)
        {
            EnableBroadcast = true,
        };

        if (MulticastInterface.FindLanAddress() is { } lanAddress)
        {
            client.Client.SetSocketOption(
                SocketOptionLevel.IP,
                SocketOptionName.MulticastInterface,
                lanAddress.GetAddressBytes());
        }

        foreach (var target in targets)
        {
            await client.SendAsync(payload, target, cancellationToken);
        }

        return await ReceiveAsync(client, timeout, cancellationToken);
    }

    public static async Task<IReadOnlyList<(byte[] Data, IPEndPoint Remote)>> ReceiveAsync(
        UdpClient client,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var results = new List<(byte[] Data, IPEndPoint Remote)>();
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        while (true)
        {
            try
            {
                var response = await client.ReceiveAsync(timeoutSource.Token);
                results.Add((response.Buffer, response.RemoteEndPoint));
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException)
            {
                break;
            }
        }

        return results;
    }
}
