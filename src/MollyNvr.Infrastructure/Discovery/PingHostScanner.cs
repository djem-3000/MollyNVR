using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using MollyNvr.Application.Discovery;

namespace MollyNvr.Infrastructure.Discovery;

/// <summary>Поиск живых адресов ICMP echo (без прав — <c>null</c>, вызывающий код делает полный скан).</summary>
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
                        // хост не ответил
                    }
                    catch (SocketException)
                    {
                        Interlocked.Increment(ref permissionErrors);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        // таймаут одного хоста
                    }
                });
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }

        // Ни один хост не ответил, а каждый вызов упал с ошибкой сокета — ICMP недоступен.
        return permissionErrors > 0 && alive.IsEmpty ? null : alive.ToHashSet();
    }
}
