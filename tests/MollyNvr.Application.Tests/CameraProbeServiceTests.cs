using System.Net;
using MollyNvr.Application.Cameras;
using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Application.Tests;

public class CameraProbeServiceTests
{
    private static readonly CameraEndpoint Endpoint = new(IPAddress.Parse("192.168.1.10"));
    private static readonly CameraCredentials Credentials = new("admin", "secret");
    private static readonly CameraTarget Target = new(Endpoint, Credentials);

    [Fact]
    public async Task ProbeAsync_UsesFirstModuleThatCanHandleEndpoint()
    {
        var first = new FakeModule("first", canHandle: false);
        var second = new FakeModule("second", canHandle: true);
        var service = CreateService(first, second);

        var result = await service.ProbeAsync(Target, CancellationToken.None);

        Assert.Equal("second", result.VendorKey);
        Assert.Equal(1, second.ProbeCalls);
        Assert.Equal(0, first.ProbeCalls);
    }

    [Fact]
    public async Task ProbeAsync_UsesVendorHintAndSkipsDetection()
    {
        var first = new FakeModule("first", canHandle: false);
        var second = new FakeModule("second", canHandle: false);
        var service = CreateService(first, second);
        var target = Target with { VendorKey = "second" };

        var result = await service.ProbeAsync(target, CancellationToken.None);

        Assert.Equal("second", result.VendorKey);
        Assert.Equal(1, second.ProbeCalls);
        Assert.Equal(0, first.CanHandleCalls);
    }

    [Fact]
    public async Task ProbeAsync_ThrowsForUnknownVendor()
    {
        var service = CreateService(new FakeModule("first", canHandle: true));
        var target = Target with { VendorKey = "unknown" };

        await Assert.ThrowsAsync<CameraOperationNotSupportedException>(
            () => service.ProbeAsync(target, CancellationToken.None));
    }

    [Fact]
    public async Task ProbeAsync_PrefersAuthenticationErrorOverUnavailable()
    {
        var unavailable = new FakeModule("unavailable", canHandle: true)
        {
                        ProbeException = new CameraUnavailableException("no response"),
        };
        var unauthorized = new FakeModule("unauthorized", canHandle: true)
        {
                        ProbeException = new CameraAuthenticationException("wrong password"),
        };
        var service = CreateService(unavailable, unauthorized);

        await Assert.ThrowsAsync<CameraAuthenticationException>(
            () => service.ProbeAsync(Target, CancellationToken.None));
    }

    [Fact]
    public async Task ProbeAsync_EnrichesProfilesWithSdpCodecs()
    {
        var module = new FakeModule("fake", canHandle: true);
        var mediaProbe = new FakeMediaProbe(new RtspMediaInfo("H.265", "AAC", 16000));
        var service = CreateService([module], mediaProbe, new FakeFrameGrabber([]));

        var result = await service.ProbeAsync(Target, CancellationToken.None);

        Assert.All(result.Profiles, profile => Assert.Equal("H.265", profile.VideoCodec));
        Assert.All(result.Profiles, profile => Assert.Equal("AAC", profile.AudioCodec));
        Assert.All(result.Profiles, profile => Assert.Equal(16000, profile.AudioSampleRate));
    }

    [Fact]
    public async Task ProbeAsync_KeepsModuleCodecsWhenSdpUnavailable()
    {
        var module = new FakeModule("fake", canHandle: true);
        var mediaProbe = new FakeMediaProbe(null);
        var service = CreateService([module], mediaProbe, new FakeFrameGrabber([]));

        var result = await service.ProbeAsync(Target, CancellationToken.None);

        Assert.All(result.Profiles, profile => Assert.Equal("H.264", profile.VideoCodec));
    }

    [Fact]
    public async Task GetSnapshotAsync_UsesNativeSnapshotWhenAvailable()
    {
        var module = new FakeModule("fake", canHandle: true) { Snapshot = [1, 2, 3] };
        var grabber = new FakeFrameGrabber([9]);
        var service = CreateService([module], new FakeMediaProbe(null), grabber);

        var snapshot = await service.GetSnapshotAsync(Target, CameraStreamProfileKind.Sub, profileId: null, CancellationToken.None);

        Assert.Equal([1, 2, 3], snapshot);
        Assert.Null(grabber.LastSourceUrl);
    }

    [Fact]
    public async Task GetSnapshotAsync_FallsBackToRtspFrameWithCredentials()
    {
        var module = new FakeModule("fake", canHandle: true)
        {
                        SnapshotException = new CameraOperationNotSupportedException("no HTTP"),
        };
        var grabber = new FakeFrameGrabber([7, 8]);
        var service = CreateService([module], new FakeMediaProbe(null), grabber);

        var snapshot = await service.GetSnapshotAsync(Target, CameraStreamProfileKind.Sub, profileId: null, CancellationToken.None);

        Assert.Equal([7, 8], snapshot);
        Assert.Equal("rtsp://admin:secret@192.168.1.10:554/stream/sub", grabber.LastSourceUrl);
    }

    [Fact]
    public async Task GetSnapshotAsync_ThrowsWhenProfileMissing()
    {
        var module = new FakeModule("fake", canHandle: true) { IncludeSubProfile = false };
        var service = CreateService([module], new FakeMediaProbe(null), new FakeFrameGrabber([]));

        await Assert.ThrowsAsync<CameraOperationNotSupportedException>(
            () => service.GetSnapshotAsync(Target, CameraStreamProfileKind.Sub, profileId: null, CancellationToken.None));
    }

    [Fact]
    public async Task ProbeView_MasksCredentialsInRtspUrl()
    {
        var module = new FakeModule("fake", canHandle: true) { IncludeCredentialsInUrl = true };
        var service = CreateService([module], new FakeMediaProbe(null), new FakeFrameGrabber([]));

        var result = await service.ProbeAsync(Target, CancellationToken.None);

        Assert.All(result.Profiles, profile => Assert.DoesNotContain("secret", profile.RtspUrl, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProbeView_ReportsHikvisionSdkOn8000InsteadOfOnvif()
    {
        var service = CreateService(new FakeModule("hikvision", canHandle: true));
        var target = Target with { VendorKey = "hikvision" };

        var result = await service.ProbeAsync(target, CancellationToken.None);

        Assert.Collection(
            result.Protocols,
            protocol =>
            {
                Assert.Equal("HTTP, ISAPI", protocol.Name);
                Assert.Equal(80, protocol.Port);
                Assert.Equal("control", protocol.Role);
            },
            protocol =>
            {
                Assert.Equal("RTSP", protocol.Name);
                Assert.Equal(554, protocol.Port);
                Assert.Equal("video", protocol.Role);
            },
            protocol =>
            {
                Assert.Equal("SDK", protocol.Name);
                Assert.Equal(8000, protocol.Port);
                Assert.Equal("control", protocol.Role);
            });
        Assert.DoesNotContain(result.Protocols, protocol => protocol.Name == "ONVIF");
    }

    [Fact]
    public async Task ProbeView_ReportsONVIFOnvifOn8000()
    {
        var module = new FakeModule("onvif", canHandle: true);
        var service = CreateService(
            [module],
            new FakeMediaProbe(null),
            new FakeFrameGrabber([]),
            new FakePortScanner(554, 8000));
        var target = Target with { VendorKey = "onvif" };

        var result = await service.ProbeAsync(target, CancellationToken.None);

        Assert.Collection(
            result.Protocols,
            protocol =>
            {
                Assert.Equal("RTSP", protocol.Name);
                Assert.Equal(554, protocol.Port);
                Assert.Equal("video", protocol.Role);
            },
            protocol =>
            {
                Assert.Equal("ONVIF", protocol.Name);
                Assert.Equal(8000, protocol.Port);
                Assert.Equal("control", protocol.Role);
            });
    }

    [Fact]
    public async Task ProbeView_ShowsUnknownPortWithoutProtocolName()
    {
        var module = new FakeModule("onvif", canHandle: true);
        var service = CreateService(
            [module],
            new FakeMediaProbe(null),
            new FakeFrameGrabber([]),
            new FakePortScanner(8080));

        var result = await service.ProbeAsync(Target, CancellationToken.None);

        var protocol = Assert.Single(result.Protocols);
        Assert.Null(protocol.Name);
        Assert.Equal(8080, protocol.Port);
        Assert.Null(protocol.Role);
    }

    [Fact]
    public async Task ProbeView_NoOpenPorts_ReturnsEmptyProtocols()
    {
        var module = new FakeModule("onvif", canHandle: true);
        var service = CreateService(
            [module],
            new FakeMediaProbe(null),
            new FakeFrameGrabber([]),
            new FakePortScanner());

        var result = await service.ProbeAsync(Target, CancellationToken.None);

        Assert.Empty(result.Protocols);
    }

    [Fact]
    public async Task ProbeAsync_ReolinkPrefersOnvifWhenAvailable()
    {
        var onvif = new FakeModule("onvif", canHandle: true);
        var reolink = new FakeModule("reolink", canHandle: true);
        var service = CreateService(onvif, reolink);
        var target = Target with { VendorKey = "reolink" };

        var result = await service.ProbeAsync(target, CancellationToken.None);

        Assert.Equal("onvif", result.VendorKey);
        Assert.Equal(1, onvif.ProbeCalls);
        Assert.Equal(0, reolink.ProbeCalls);
    }

    [Fact]
    public async Task ProbeAsync_ReolinkFallsBackToBaichuanWhenOnvifFails()
    {
        var onvif = new FakeModule("onvif", canHandle: true)
        {
                        ProbeException = new CameraUnavailableException("ONVIF unavailable"),
        };
        var reolink = new FakeModule("reolink", canHandle: true);
        var service = CreateService(onvif, reolink);
        var target = Target with { VendorKey = "reolink" };

        var result = await service.ProbeAsync(target, CancellationToken.None);

        Assert.Equal("reolink", result.VendorKey);
        Assert.Equal(1, onvif.ProbeCalls);
        Assert.Equal(1, reolink.ProbeCalls);
    }

    private static CameraProbeService CreateService(params ICameraModule[] modules) =>
        CreateService(modules, new FakeMediaProbe(null), new FakeFrameGrabber([]));

    private static CameraProbeService CreateService(
        IReadOnlyList<ICameraModule> modules,
        IRtspMediaProbe mediaProbe,
        IFrameGrabber frameGrabber,
        IPortScanner? portScanner = null) =>
        new(modules, mediaProbe, frameGrabber, portScanner ?? new FakePortScanner(80, 554, 8000));

    private static CameraProbeService CreateService(
        ICameraModule module,
        IRtspMediaProbe mediaProbe,
        IFrameGrabber frameGrabber) =>
        CreateService([module], mediaProbe, frameGrabber);

    private sealed class FakeModule(string key, bool canHandle) : ICameraModule
    {
        public string Key => key;

        public string DisplayName => key;

        public int CanHandleCalls { get; private set; }

        public int ProbeCalls { get; private set; }

        public CameraModuleException? ProbeException { get; init; }

        public CameraModuleException? SnapshotException { get; init; }

        public byte[] Snapshot { get; init; } = [];

        public bool IncludeSubProfile { get; init; } = true;

        public bool IncludeCredentialsInUrl { get; init; }

        public Task<bool> CanHandleAsync(CameraEndpoint endpoint, CancellationToken cancellationToken)
        {
            CanHandleCalls++;
            return Task.FromResult(canHandle);
        }

        public Task<CameraProbeResult> ProbeAsync(
            CameraEndpoint endpoint,
            CameraCredentials credentials,
            CancellationToken cancellationToken)
        {
            ProbeCalls++;

            if (ProbeException is not null)
            {
                throw ProbeException;
            }

            return Task.FromResult(CreateResult(endpoint));
        }

        public Task<byte[]> GetSnapshotAsync(
            CameraEndpoint endpoint,
            CameraCredentials credentials,
            CameraProfile profile,
            CancellationToken cancellationToken)
        {
            if (SnapshotException is not null)
            {
                throw SnapshotException;
            }

            return Task.FromResult(Snapshot);
        }

        private CameraProbeResult CreateResult(CameraEndpoint endpoint)
        {
            var credentials = IncludeCredentialsInUrl ? "admin:secret@" : string.Empty;

            var main = new CameraProfile(
                CameraStreamProfileKind.Main,
                "main",
                "Main",
                new CameraVideoSettings("H.264", 1920, 1080, 25, 4096),
                new CameraAudioSettings(true, "G.711ulaw", 8000, 64),
                $"rtsp://{credentials}{endpoint.Host}:{endpoint.RtspPort}/stream/main");

            var profiles = new List<CameraProfile> { main };

            if (IncludeSubProfile)
            {
                profiles.Add(new CameraProfile(
                    CameraStreamProfileKind.Sub,
                    "sub",
                    "Sub",
                    new CameraVideoSettings("H.264", 640, 360, 10, 512),
                    new CameraAudioSettings(false, null, null, null),
                    $"rtsp://{credentials}{endpoint.Host}:{endpoint.RtspPort}/stream/sub"));
            }

            return new CameraProbeResult(
                new CameraDeviceInfo("Fake", "Model", "1.0", "SN", "Camera"),
                profiles);
        }
    }

    private sealed class FakePortScanner(params int[] openPorts) : IPortScanner
    {
        public Task<IReadOnlyList<int>> FindOpenPortsAsync(
            IPAddress host,
            IReadOnlyList<int> ports,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<int>>(
                openPorts.Where(ports.Contains).Order().ToList());
    }

    private sealed class FakeMediaProbe(RtspMediaInfo? info) : IRtspMediaProbe
    {
        public Task<RtspMediaInfo?> ProbeAsync(
            Uri rtspUri,
            CameraCredentials credentials,
            CancellationToken cancellationToken) =>
            Task.FromResult(info);
    }

    private sealed class FakeFrameGrabber(byte[] frame) : IFrameGrabber
    {
        public string? LastSourceUrl { get; private set; }

        public Task<byte[]> GrabJpegAsync(string sourceUrl, CancellationToken cancellationToken)
        {
            LastSourceUrl = sourceUrl;
            return Task.FromResult(frame);
        }
    }
}
