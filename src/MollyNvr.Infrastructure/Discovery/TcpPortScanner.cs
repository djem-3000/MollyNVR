using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using MollyNvr.Application.Cameras;

namespace MollyNvr.Infrastructure.Discovery;

/// <summary>Scanning of typical device TCP ports during a camera probe.</summary>
public sealed class TcpPortScanner : IPortScanner
{
    private const int MaxConcurrency = 16;
    private const int Attempts = 2;

    private static readonly TimeSpan PortTimeout = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(300);

    public async Task<IReadOnlyList<int>> FindOpenPortsAsync(
        IPAddress host,
        IReadOnlyList<int> ports,
        CancellationToken cancellationToken)
    {
        var openPorts = new ConcurrentBag<int>();

        await Parallel.ForEachAsync(
            ports.Distinct(),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxConcurrency,
                CancellationToken = cancellationToken,
            },
            async (port, token) =>
            {
                if (await IsPortOpenAsync(host, port, token))
                {
                    openPorts.Add(port);
                }
            });

        return openPorts.Order().ToList();
    }

    /// <summary>
        /// Cameras (especially Reolink) sometimes do not answer SYN — the connection is not rejected
        /// but silently dropped. Such a timeout is retried once; an explicit refusal (RST) is not retried.
    /// </summary>
    private static async Task<bool> IsPortOpenAsync(
        IPAddress host,
        int port,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            var state = await TryConnectAsync(host, port, cancellationToken);

            if (state == PortState.Open)
            {
                return true;
            }

            if (state == PortState.Refused)
            {
                return false;
            }

            if (attempt < Attempts)
            {
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }

        return false;
    }

    private static async Task<PortState> TryConnectAsync(
        IPAddress host,
        int port,
        CancellationToken cancellationToken)
    {
        using var client = new TcpClient(host.AddressFamily);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(PortTimeout);

        try
        {
            await client.ConnectAsync(host, port, timeoutSource.Token);
            return PortState.Open;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PortState.Timeout;
        }
        catch (SocketException exception)
        {
            return exception.SocketErrorCode is SocketError.ConnectionRefused
                or SocketError.HostUnreachable
                or SocketError.NetworkUnreachable
                or SocketError.AccessDenied
                ? PortState.Refused
                : PortState.Timeout;
        }
    }

    private enum PortState
    {
        Open,
        Refused,
        Timeout,
    }
}
