using System.Net;
using System.Net.Sockets;
using MollyNvr.Application.Cameras;
using MollyNvr.Application.Discovery;
using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Application.Tests;

public class DiscoveryServiceTests
{
    [Fact]
    public async Task ScanUnicastAsync_IdentifiesVendorByModule()
    {
        var scanner = new FakePortScanner(new Dictionary<string, int[]>
        {
            ["192.168.1.115"] = [80, 554],
        });
        var module = new FakeModule("hikvision", canHandle: true);
        var service = CreateService(scanner, [module]);

        var devices = await service.ScanUnicastAsync("192.168.1.0/24", CancellationToken.None);

        var device = Assert.Single(devices);
        Assert.Equal("192.168.1.115", device.Host);
        Assert.Equal("hikvision", device.VendorKey);
        Assert.Equal("Hikvision", device.Vendor);
        Assert.Equal([80, 554], device.OpenPorts);
    }

    [Fact]
    public async Task ScanUnicastAsync_UsesOuiVendorWhenMacAvailable()
    {
        var scanner = new FakePortScanner(new Dictionary<string, int[]>
        {
            ["192.168.1.115"] = [554],
        });
        var arp = new FakeArpTable(new Dictionary<string, string>
        {
            ["192.168.1.115"] = "44:19:B6:00:00:01",
        });
        var service = CreateService(
            scanner,
            [new FakeModule("hikvision", canHandle: false)],
            arpTable: arp,
            oui: new FakeOuiLookup("Hikvision"));

        var device = Assert.Single(await service.ScanUnicastAsync("192.168.1.115", CancellationToken.None));

        Assert.Equal("44:19:B6:00:00:01", device.Mac);
        Assert.Equal("Hikvision", device.Vendor);
        Assert.Equal("hikvision", device.VendorKey);
    }

    [Fact]
    public async Task ScanUnicastAsync_ReturnsUnknownVendorWhenNoModuleMatches()
    {
        var scanner = new FakePortScanner(new Dictionary<string, int[]>
        {
            ["192.168.1.50"] = [554],
        });
        var service = CreateService(scanner, [new FakeModule("hikvision", canHandle: false)]);

        var device = Assert.Single(await service.ScanUnicastAsync("192.168.1.0/24", CancellationToken.None));

        Assert.Null(device.VendorKey);
        Assert.Null(device.Vendor);
    }

    [Fact]
    public async Task ScanUnicastAsync_SortsDevicesByAddress()
    {
        var scanner = new FakePortScanner(new Dictionary<string, int[]>
        {
            ["192.168.1.20"] = [554],
            ["192.168.1.3"] = [554],
        });
        var service = CreateService(scanner, []);

        var devices = await service.ScanUnicastAsync("192.168.1.0/24", CancellationToken.None);

        Assert.Equal(["192.168.1.3", "192.168.1.20"], devices.Select(static device => device.Host));
    }

    [Fact]
    public async Task ScanUnicastAsync_ModuleThrows_ContinuesWithNextModule()
    {
        var scanner = new FakePortScanner(new Dictionary<string, int[]>
        {
            ["192.168.1.113"] = [554, 8000],
        });
        var broken = new FakeModule("onvif", canHandle: false) { Throws = true };
        var working = new FakeModule("onvif", canHandle: true);
        var service = CreateService(scanner, [broken, working]);

        var device = Assert.Single(await service.ScanUnicastAsync("192.168.1.0/24", CancellationToken.None));

        Assert.Equal("onvif", device.VendorKey);
    }

    [Fact]
    public async Task ScanUnicastAsync_IgnoresHttpOnlyDevices()
    {
        var scanner = new FakePortScanner(new Dictionary<string, int[]>
        {
            ["192.168.1.1"] = [80, 443],
        });
        var service = CreateService(scanner, [new FakeModule("hikvision", canHandle: true)]);

        Assert.Empty(await service.ScanUnicastAsync("192.168.1.1", CancellationToken.None));
    }

    [Fact]
    public async Task ScanUnicastAsync_IncludesAlreadySavedCameras()
    {
        var scanner = new FakePortScanner(new Dictionary<string, int[]>
        {
            ["192.168.1.113"] = [554],
            ["192.168.1.115"] = [554],
        });
        var service = CreateService(scanner, [new FakeModule("onvif", canHandle: true)]);

        var devices = await service.ScanUnicastAsync("192.168.1.0/24", CancellationToken.None);

        Assert.Equal(
            ["192.168.1.113", "192.168.1.115"],
            devices.Select(static device => device.Host));
    }

    [Fact]
    public async Task ScanUnicastAsync_ScansOnlyAliveHosts()
    {
        var scanner = new FakePortScanner(new Dictionary<string, int[]>
        {
            ["192.168.1.113"] = [554],
            ["192.168.1.115"] = [554],
        });
        var service = CreateService(
            scanner,
            [],
            aliveHosts: new FakeAliveHostScanner("192.168.1.115"));

        var devices = await service.ScanUnicastAsync("192.168.1.0/24", CancellationToken.None);

        Assert.Equal(["192.168.1.115"], devices.Select(static device => device.Host));
    }

    [Fact]
    public async Task ScanUnicastAsync_RetriesWholeRangeWhenAliveHostsFindNothing()
    {
        var scanner = new FakePortScanner(new Dictionary<string, int[]>
        {
            ["192.168.1.115"] = [554],
        });
        var service = CreateService(
            scanner,
            [],
            aliveHosts: new FakeAliveHostScanner("192.168.1.200"));

        var device = Assert.Single(await service.ScanUnicastAsync("192.168.1.0/24", CancellationToken.None));

        Assert.Equal("192.168.1.115", device.Host);
    }

    [Fact]
    public async Task ScanUnicastAsync_InvalidRange_Throws()
    {
        var service = CreateService(new FakePortScanner(new Dictionary<string, int[]>()), []);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ScanUnicastAsync("not-a-range", CancellationToken.None));
    }

    [Fact]
    public async Task ScanMulticastAsync_MergesProtocolResponses()
    {
        var responses = new FakeProtocol(
            new DiscoveryResponse(IPAddress.Parse("192.168.1.20"), VendorKey: "onvif", Vendor: "ONVIF"),
            new DiscoveryResponse(IPAddress.Parse("192.168.1.21"), VendorKey: "hikvision", Vendor: "Hikvision"));
        var scanner = new FakePortScanner(new Dictionary<string, int[]>
        {
            ["192.168.1.20"] = [80, 554],
            ["192.168.1.21"] = [8000],
        });
        var service = CreateService(
            scanner,
            [new FakeModule("onvif", canHandle: true)],
            protocols: [responses]);

        var devices = await service.ScanMulticastAsync(CancellationToken.None);

        Assert.Equal(["192.168.1.20", "192.168.1.21"], devices.Select(static device => device.Host));
    }

    [Fact]
    public async Task ScanMulticastAsync_ProtocolFailure_DoesNotBreakOtherProtocols()
    {
        var broken = new ThrowingProtocol();
        var working = new FakeProtocol(
            new DiscoveryResponse(IPAddress.Parse("192.168.1.30"), Vendor: "Hikvision", VendorKey: "hikvision"));
        var scanner = new FakePortScanner(new Dictionary<string, int[]>
        {
            ["192.168.1.30"] = [554],
        });
        var service = CreateService(scanner, [], protocols: [broken, working]);

        var device = Assert.Single(await service.ScanMulticastAsync(CancellationToken.None));

        Assert.Equal("192.168.1.30", device.Host);
    }

    [Fact]
    public async Task ScanMulticastAsync_PrefersOuiVendorOverOnvifProtocolLabel()
    {
        var responses = new FakeProtocol(
            new DiscoveryResponse(
                IPAddress.Parse("192.168.1.110"),
                VendorKey: "onvif",
                Vendor: "ONVIF",
                Model: "IPC-BO"));
        var scanner = new FakePortScanner(new Dictionary<string, int[]>
        {
            ["192.168.1.110"] = [80, 554, 8000],
        });
        var arp = new FakeArpTable(new Dictionary<string, string>
        {
            ["192.168.1.110"] = "EC:71:DB:00:87:1D",
        });
        var service = CreateService(
            scanner,
            [new FakeModule("onvif", canHandle: true)],
            protocols: [responses],
            arpTable: arp,
            oui: new FakeOuiLookup("REOLINK"));

        var device = Assert.Single(await service.ScanMulticastAsync(CancellationToken.None));

        Assert.Equal("REOLINK", device.Vendor);
        Assert.Equal("onvif", device.VendorKey);
        Assert.Equal("IPC-BO", device.Model);
    }

    [Fact]
    public async Task ScanMulticastAsync_ScansArpNeighboursWithoutProtocolResponse()
    {
        var scanner = new FakePortScanner(new Dictionary<string, int[]>
        {
            ["192.168.1.116"] = [9000],
        });
        var arp = new FakeArpTable(new Dictionary<string, string>
        {
            ["192.168.1.116"] = "44:01:BB:40:4C:74",
        });
        var service = CreateService(
            scanner,
            [],
            arpTable: arp,
            oui: new FakeOuiLookup("REOLINK"));

        var device = Assert.Single(await service.ScanMulticastAsync(CancellationToken.None));

        Assert.Equal("192.168.1.116", device.Host);
        Assert.Equal("REOLINK", device.Vendor);
        Assert.Equal([9000], device.OpenPorts);
    }

    [Fact]
    public async Task ScanMulticastAsync_IgnoresDevicesWithoutCameraPorts()
    {
        var responses = new FakeProtocol(
            new DiscoveryResponse(
                IPAddress.Parse("192.168.1.1"),
                Model: "Web control panel on Keenetic_Viva"));
        var scanner = new FakePortScanner(new Dictionary<string, int[]>
        {
            ["192.168.1.1"] = [80, 443],
        });
        var service = CreateService(
            scanner,
            [new FakeModule("hikvision", canHandle: true)],
            protocols: [responses]);

        Assert.Empty(await service.ScanMulticastAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ScanAddressAsync_UsesSelectedVendor()
    {
        var scanner = new FakePortScanner(new Dictionary<string, int[]>
        {
            ["192.168.1.40"] = [80, 8000],
        });
        var service = CreateService(scanner, [new FakeModule("hikvision", canHandle: false)]);

        var device = Assert.Single(
            await service.ScanAddressAsync("192.168.1.40", "hikvision", CancellationToken.None));

        Assert.Equal("hikvision", device.VendorKey);
        Assert.Equal("Hikvision", device.Vendor);
        Assert.Equal([80, 8000], device.OpenPorts);
    }

    [Fact]
    public async Task ScanAddressAsync_InvalidAddress_Throws()
    {
        var service = CreateService(new FakePortScanner(new Dictionary<string, int[]>()), []);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ScanAddressAsync("not-an-ip", null, CancellationToken.None));
    }

    private static DiscoveryService CreateService(
        FakePortScanner scanner,
        IReadOnlyList<ICameraModule> modules,
        IReadOnlyList<IDiscoveryProtocol>? protocols = null,
        IArpTableResolver? arpTable = null,
        IOuiLookup? oui = null,
        IAliveHostScanner? aliveHosts = null) =>
        new(
            scanner,
            protocols ?? [],
            arpTable ?? new FakeArpTable(),
            oui ?? new FakeOuiLookup(),
            modules,
            aliveHosts ?? new FakeAliveHostScanner());

    private sealed class FakeAliveHostScanner(params string[] alive) : IAliveHostScanner
    {
        public Task<IReadOnlySet<IPAddress>?> FindAliveHostsAsync(
            IReadOnlyList<IPAddress> hosts,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<IPAddress>?>(
                alive.Length == 0
                    ? null
                    : alive.Select(IPAddress.Parse).ToHashSet());
    }

    private sealed class FakePortScanner(IReadOnlyDictionary<string, int[]> openPorts) : IPortScanner
    {
        public Task<IReadOnlyList<int>> FindOpenPortsAsync(
            IPAddress host,
            IReadOnlyList<int> ports,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<int>>(
                openPorts.TryGetValue(host.ToString(), out var found)
                    ? found.Where(ports.Contains).Order().ToList()
                    : []);
    }

    private sealed class FakeArpTable(IReadOnlyDictionary<string, string>? entries = null)
        : IArpTableResolver
    {
        public Task<IReadOnlyDictionary<IPAddress, string>> GetMacAddressesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<IPAddress, string>>(
                (entries ?? new Dictionary<string, string>())
                .ToDictionary(static pair => IPAddress.Parse(pair.Key), static pair => pair.Value));
    }

    private sealed class FakeOuiLookup(string? vendor = null) : IOuiLookup
    {
        public string? FindVendor(string? macAddress) => vendor;
    }

    private sealed class FakeProtocol(params DiscoveryResponse[] responses) : IDiscoveryProtocol
    {
        public string Name => "fake";

        public Task<IReadOnlyList<DiscoveryResponse>> DiscoverAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DiscoveryResponse>>(responses);
    }

    private sealed class ThrowingProtocol : IDiscoveryProtocol
    {
        public string Name => "broken";

        public Task<IReadOnlyList<DiscoveryResponse>> DiscoverAsync(CancellationToken cancellationToken) =>
            throw new SocketException();
    }

    private sealed class FakeModule(string key, bool canHandle) : ICameraModule
    {
        public string Key => key;

        public string DisplayName => key == "hikvision" ? "Hikvision" : "ONVIF";

        public bool Throws { get; init; }

        public Task<bool> CanHandleAsync(CameraEndpoint endpoint, CancellationToken cancellationToken)
        {
            if (Throws)
            {
                throw new HttpRequestException("сбой");
            }

            return Task.FromResult(canHandle);
        }

        public Task<CameraProbeResult> ProbeAsync(
            CameraEndpoint endpoint,
            CameraCredentials credentials,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<byte[]> GetSnapshotAsync(
            CameraEndpoint endpoint,
            CameraCredentials credentials,
            CameraProfile profile,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
