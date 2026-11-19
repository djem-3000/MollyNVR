using System.Net;
using System.Net.Sockets;
using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Cameras.Reolink;

/// <summary>
/// Reolink cameras without HTTP/ONVIF: communication goes over proprietary Baichuan (port 9000)
/// through the neolink.net bridge, which exposes regular RTSP.
/// </summary>
public sealed class ReolinkCameraModule(ReolinkBridgeClient bridge)
    : ICameraModule, ICameraNetworkInfoProvider
{
        /// <summary>Module key.</summary>
    public const string ModuleKey = "reolink";

    private const int BaichuanPort = 9000;
    private const int DefaultAudioSampleRate = 16000;

    private static readonly TimeSpan PortTimeout = TimeSpan.FromMilliseconds(1500);

    /// <inheritdoc />
    public string Key => ModuleKey;

    /// <inheritdoc />
    public string DisplayName => "Reolink";

    /// <inheritdoc />
    public async Task<bool> CanHandleAsync(CameraEndpoint endpoint, CancellationToken cancellationToken) =>
        await IsPortOpenAsync(endpoint.Address, BaichuanPort, cancellationToken)
        && await bridge.IsAvailableAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<CameraProbeResult> ProbeAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CancellationToken cancellationToken)
    {
        var cameraName = ReolinkBridgeClient.CameraName(endpoint.Address);

        try
        {
            await bridge.ProvisionAsync(endpoint.Address, credentials, cancellationToken);
        }
        catch (ReolinkBridgeException exception)
        {
            throw new CameraUnavailableException(exception.Message, exception);
        }

        if (!await bridge.WaitUntilOnlineAsync(cameraName, cancellationToken))
        {
            throw new CameraUnavailableException(
                                $"Camera {endpoint.Host} did not respond over the Reolink protocol (port {BaichuanPort}). " +
                                "Check the login, password and camera availability.");
        }

        var streamInfo = await bridge.GetStreamInfoAsync(cameraName, cancellationToken);
        var profiles = BuildProfiles(cameraName, streamInfo);

        if (profiles.Count == 0)
        {
            throw new CameraUnavailableException(
                                $"Camera {endpoint.Host} reported no stream profile.");
        }

        var device = await bridge.GetDeviceInfoAsync(cameraName, cancellationToken);

        return new CameraProbeResult(
            new CameraDeviceInfo(
                Vendor: DisplayName,
                Model: device?.Model ?? device?.Name ?? "Reolink",
                Firmware: device?.Firmware ?? string.Empty,
                SerialNumber: string.IsNullOrWhiteSpace(device?.Serial) ? null : device.Serial,
                DeviceName: null),
            profiles);
    }

    /// <inheritdoc />
    public async Task<byte[]> GetSnapshotAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CameraProfile profile,
        CancellationToken cancellationToken)
    {
        var cameraName = ReolinkBridgeClient.CameraName(endpoint.Address);
        var snapshot = await bridge.GetSnapshotAsync(cameraName, cancellationToken);

        return snapshot ?? throw new CameraModuleException(
                        $"The Reolink bridge did not return a snapshot of camera {endpoint.Host}.");
    }

    /// <inheritdoc />
    public Task<bool?> GetDhcpEnabledAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CancellationToken cancellationToken) =>
        Task.FromResult<bool?>(null);

    private List<CameraProfile> BuildProfiles(string cameraName, BridgeStreamInfo? streamInfo)
    {
        var profiles = new List<CameraProfile>();

        foreach (var stream in streamInfo?.Profiles ?? [])
        {
            if (ToKind(stream.Type) is not { } kind || stream.Width <= 0 || stream.Height <= 0)
            {
                continue;
            }

            profiles.Add(new CameraProfile(
                Kind: kind,
                Id: stream.Type!,
                Name: null,
                Video: new CameraVideoSettings(
                    Codec: "H.264",
                    Width: stream.Width,
                    Height: stream.Height,
                    FrameRate: stream.DefaultFramerate,
                    BitrateKbps: stream.DefaultBitrate),
                Audio: new CameraAudioSettings(
                    Enabled: false,
                    Codec: null,
                    SampleRate: DefaultAudioSampleRate,
                    Bitrate: null),
                RtspUrl: bridge.BuildRtspUrl(cameraName, stream.Type!).ToString()));
        }

        return profiles;
    }

    private static CameraStreamProfileKind? ToKind(string? stream) => stream switch
    {
        "mainStream" => CameraStreamProfileKind.Main,
        "subStream" => CameraStreamProfileKind.Sub,
        _ => null,
    };

    private static async Task<bool> IsPortOpenAsync(
        IPAddress address,
        int port,
        CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(PortTimeout);

        try
        {
            await client.ConnectAsync(address, port, timeout.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
