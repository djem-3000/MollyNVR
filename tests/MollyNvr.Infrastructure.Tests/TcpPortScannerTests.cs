using System.Net;
using System.Net.Sockets;
using MollyNvr.Infrastructure.Discovery;

namespace MollyNvr.Infrastructure.Tests;

public class TcpPortScannerTests
{
    [Fact]
    public async Task FindOpenPortsAsync_ReturnsOnlyListeningPorts()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var openPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        var closedPort = GetFreePort();
        var scanner = new TcpPortScanner();

        var ports = await scanner.FindOpenPortsAsync(
            IPAddress.Loopback,
            [openPort, closedPort],
            CancellationToken.None);

        Assert.Contains(openPort, ports);
        Assert.DoesNotContain(closedPort, ports);
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
