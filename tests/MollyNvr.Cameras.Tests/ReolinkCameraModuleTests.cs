using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using MollyNvr.Cameras.Abstractions;
using MollyNvr.Cameras.Reolink;

namespace MollyNvr.Cameras.Tests;

public class ReolinkCameraModuleTests
{
    private static readonly CameraEndpoint Endpoint = new(IPAddress.Parse("192.168.1.10"));
    private static readonly CameraCredentials Credentials = new("user", "pass");

    [Fact]
    public async Task ProbeAsync_MapsStreamInfoToProfiles()
    {
        using var workspace = new ConfigWorkspace();
        var module = CreateModule(workspace, HandleCamera);

        var result = await module.ProbeAsync(Endpoint, Credentials, CancellationToken.None);

        Assert.Equal("Reolink", result.Device.Vendor);
        Assert.Equal("E1", result.Device.Model);
        Assert.Equal("v1.2.3", result.Device.Firmware);
        Assert.Null(result.Device.SerialNumber);
        Assert.Null(result.Device.DeviceName);

        var main = Assert.IsType<CameraProfile>(result.MainProfile);
        Assert.Equal("mainStream", main.Id);
        Assert.Equal(CameraStreamProfileKind.Main, main.Kind);
        Assert.Equal(2304, main.Video.Width);
        Assert.Equal(1296, main.Video.Height);
        Assert.Equal(15, main.Video.FrameRate);
        Assert.Equal(2560, main.Video.BitrateKbps);
        Assert.Equal("rtsp://bridge:8654/cam-192-168-1-10/mainStream", main.RtspUrl);

        var sub = Assert.IsType<CameraProfile>(result.SubProfile);
        Assert.Equal("subStream", sub.Id);
        Assert.Equal(384, sub.Video.BitrateKbps);
        Assert.Equal("rtsp://bridge:8654/cam-192-168-1-10/subStream", sub.RtspUrl);
    }

    [Fact]
    public async Task ProbeAsync_WritesCameraWithCredentialsToBridgeConfig()
    {
        using var workspace = new ConfigWorkspace();
        var module = CreateModule(workspace, HandleCamera);

        await module.ProbeAsync(Endpoint, Credentials, CancellationToken.None);

        var root = JsonNode.Parse(await File.ReadAllTextAsync(workspace.ConfigPath))!.AsObject();
        var camera = Assert.Single(root["cameras"]!.AsArray())!.AsObject();

        Assert.Equal("cam-192-168-1-10", camera["name"]!.GetValue<string>());
        Assert.Equal("192.168.1.10", camera["address"]!.GetValue<string>());
        Assert.Equal("user", camera["username"]!.GetValue<string>());
        Assert.Equal("pass", camera["password"]!.GetValue<string>());
    }

    [Fact]
    public async Task ProbeAsync_CameraNeverComesOnline_ThrowsUnavailable()
    {
        using var workspace = new ConfigWorkspace();
        var module = CreateModule(
            workspace,
            _ => Json("""[{ "name": "cam-192-168-1-10", "online": false }]"""),
            TimeSpan.FromMilliseconds(300));

        var exception = await Assert.ThrowsAsync<CameraUnavailableException>(
            () => module.ProbeAsync(Endpoint, Credentials, CancellationToken.None));

        Assert.Contains("9000", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSnapshotAsync_ReturnsBridgeSnapshot()
    {
        using var workspace = new ConfigWorkspace();
        var module = CreateModule(workspace, request =>
            request.RequestUri!.AbsolutePath.EndsWith("/snapshot.jpg", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([1, 2, 3]),
                }
                : NotFound());

        var profile = new CameraProfile(
            CameraStreamProfileKind.Main,
            "mainStream",
            null,
            new CameraVideoSettings("H.264", 1920, 1080, 15, 2048),
            new CameraAudioSettings(false, null, null, null),
            "rtsp://bridge:8654/cam-192-168-1-10/mainStream");

        var snapshot = await module.GetSnapshotAsync(Endpoint, Credentials, profile, CancellationToken.None);

        Assert.Equal([1, 2, 3], snapshot);
    }

    [Fact]
    public async Task IsAvailableAsync_BridgeThrows_ReturnsFalse()
    {
        using var workspace = new ConfigWorkspace();
        var bridge = CreateBridge(workspace, _ => throw new HttpRequestException("нет моста"));

        Assert.False(await bridge.IsAvailableAsync(CancellationToken.None));
    }

    private static ReolinkCameraModule CreateModule(
        ConfigWorkspace workspace,
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        TimeSpan? onlineTimeout = null) =>
        new(CreateBridge(workspace, respond, onlineTimeout));

    private static ReolinkBridgeClient CreateBridge(
        ConfigWorkspace workspace,
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        TimeSpan? onlineTimeout = null)
    {
        var options = new ReolinkBridgeOptions
        {
            ApiBaseUrl = new Uri("http://bridge:8655/"),
            RtspBaseUrl = new Uri("rtsp://bridge:8654/"),
            ConfigPath = workspace.ConfigPath,
            OnlineTimeout = onlineTimeout ?? TimeSpan.FromSeconds(2),
        };

        return new ReolinkBridgeClient(new HttpClient(new StubHandler(respond)), options);
    }

    private static HttpResponseMessage HandleCamera(HttpRequestMessage request) =>
        request.RequestUri!.AbsolutePath switch
        {
            "/api/cameras" => Json("""[{ "name": "cam-192-168-1-10", "online": true }]"""),
            "/api/cameras/cam-192-168-1-10/streaminfo" => Json("""
                {
                  "profiles": [
                    { "type": "mainStream", "width": 2304, "height": 1296, "defaultFramerate": 15, "defaultBitrate": 2560 },
                    { "type": "subStream", "width": 704, "height": 480, "defaultFramerate": 10, "defaultBitrate": 384 }
                  ]
                }
                """),
            "/api/cameras/cam-192-168-1-10/capabilities" => Json("""
                { "version": { "name": "E1", "model": "E1", "serial": "", "firmware": "v1.2.3" } }
                """),
            _ => NotFound(),
        };

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private static HttpResponseMessage NotFound() => new(HttpStatusCode.NotFound);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class ConfigWorkspace : IDisposable
    {
        private readonly string _directory =
            Path.Combine(Path.GetTempPath(), $"mollynvr-reolink-{Guid.NewGuid():N}");

        public ConfigWorkspace() => Directory.CreateDirectory(_directory);

        public string ConfigPath => Path.Combine(_directory, "config.json");

        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }
}
