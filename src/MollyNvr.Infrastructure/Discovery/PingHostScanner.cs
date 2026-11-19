using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using MollyNvr.Application.Discovery;

namespace MollyNvr.Infrastructure.Discovery;

/// <summary>ICMP echo discovery of live addresses (without privileges — <c>null</c>, the caller does a full scan).</summary>
public sealed class PingHostScanner : IAliveHostScanner
{
    private const int MaxConcurrency = 64;
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(900);

    public async Task<IReadOnlySet<IPAddress>?> FindAliveHostsAsync(
        IReadOnlyList<IPAddress> hosts,
        CancellationToken cancellationToken)
    {
        var alive = new ConcurrentBag<IPAddress>();
        var permissionErrors = 0;

        try
        {
            await Parallel.ForEachAsync(
                hosts,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = MaxConcurrency,
                    CancellationToken = cancellationToken,
                },
                async (host, token) =>
                {
                    using var ping = new Ping();

                    try
                    {
                        var reply = await ping.SendPingAsync(
                            host,
                            Timeout,
                            buffer: null,
                            options: null,
                            cancellationToken: token);
                        if (reply.Status == IPStatus.Success)
                        {
                            alive.Add(host);
                        }
                    }
                    catch (PingException)
                    {
                                                // host did not respond
                    }
                    catch (SocketException)
                    {
                        Interlocked.Increment(ref permissionErrors);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                                                // single host timeout
                    }
                });
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }

                // No host responded and every call failed with a socket error — ICMP is unavailable.
        return permissionErrors > 0 && alive.IsEmpty ? null : alive.ToHashSet();
    }
}
