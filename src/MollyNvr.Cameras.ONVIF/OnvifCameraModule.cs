using System.Net;
using System.Xml.Linq;
using MollyNvr.Cameras.Abstractions;
using MollyNvr.Cameras.Abstractions.Http;

namespace MollyNvr.Cameras.ONVIF;

/// <summary>Generic ONVIF camera module (WS-Security UsernameToken, media/device service).</summary>
public sealed class OnvifCameraModule : ICameraModule, ICameraNetworkInfoProvider, ICameraEventSource
{
        /// <summary>Module key.</summary>
    public const string ModuleKey = "onvif";

    /// <inheritdoc />
    public Task<IReadOnlyList<CameraEventType>> GetEventTypesAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CancellationToken cancellationToken) =>
        OnvifEvents.GetEventTypesAsync(endpoint, credentials, cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<CameraEvent> WatchEventsAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CancellationToken cancellationToken) =>
        OnvifEvents.WatchAsync(endpoint, credentials, cancellationToken);

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan QuickTimeout = TimeSpan.FromSeconds(3);

    /// <inheritdoc />
    public string Key => ModuleKey;

    /// <inheritdoc />
    public string DisplayName => "ONVIF";

    /// <inheritdoc />
    public async Task<bool> CanHandleAsync(CameraEndpoint endpoint, CancellationToken cancellationToken)
    {
        using var client = CreateClient(endpoint, QuickTimeout);

        try
        {
            var body = await OnvifClient.SendAsync(
                client,
                endpoint.OnvifServiceUri,
                OnvifSoap.Actions.GetDeviceInformation,
                OnvifSoap.CreateGetDeviceInformation(),
                credentials: null,
                cancellationToken);

            return OnvifSoap.IsSoapEnvelope(body);
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool?> GetDhcpEnabledAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(endpoint, ProbeTimeout);

        var xml = await CallAsync(
            client,
            endpoint.OnvifServiceUri,
            OnvifSoap.Actions.GetNetworkInterfaces,
            OnvifSoap.CreateGetNetworkInterfaces(),
            credentials,
            "GetNetworkInterfaces",
            cancellationToken);

        return OnvifXml.ParseDhcpEnabled(xml);
    }

    /// <inheritdoc />
    public async Task<CameraProbeResult> ProbeAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(endpoint, ProbeTimeout);

        var deviceXml = await CallAsync(
            client,
            endpoint.OnvifServiceUri,
            OnvifSoap.Actions.GetDeviceInformation,
            OnvifSoap.CreateGetDeviceInformation(),
            credentials,
            "GetDeviceInformation",
            cancellationToken);

        var device = OnvifXml.ParseDeviceInfo(deviceXml);
        var mediaServiceUri = await ResolveMediaServiceUriAsync(
            client,
            endpoint,
            credentials,
            cancellationToken);

        var profilesXml = await CallAsync(
            client,
            mediaServiceUri,
            OnvifSoap.Actions.GetProfiles,
            OnvifSoap.CreateGetProfiles(),
            credentials,
            "GetProfiles",
            cancellationToken);

        var profiles = new List<CameraProfile>();
        foreach (var profile in OnvifXml.ParseProfiles(profilesXml))
        {
            var streamUri = await GetStreamUriAsync(
                client,
                mediaServiceUri,
                profile.Token,
                credentials,
                cancellationToken);
            profiles.Add(ToCameraProfile(profile, streamUri));
        }

        if (profiles.Count == 0)
        {
                        throw new CameraOperationNotSupportedException("ONVIF returned no profile with a video stream.");
        }

        return new CameraProbeResult(device, AssignKinds(profiles));
    }

    /// <inheritdoc />
    public async Task<byte[]> GetSnapshotAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CameraProfile profile,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(endpoint, ProbeTimeout);

        var mediaServiceUri = await ResolveMediaServiceUriAsync(
            client,
            endpoint,
            credentials,
            cancellationToken);

        var snapshotXml = await CallAsync(
            client,
            mediaServiceUri,
            OnvifSoap.Actions.GetSnapshotUri,
            OnvifSoap.CreateGetSnapshotUri(profile.Id),
            credentials,
            "GetSnapshotUri",
            cancellationToken);

        var snapshotUri = OnvifXml.StripCredentials(OnvifXml.ParseUri(snapshotXml));

        try
        {
            return await DownloadAsync(snapshotUri, credentials, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new CameraOperationNotSupportedException(
                                "ONVIF did not provide a snapshot over HTTP — a frame from RTSP is used.");
        }
    }

    private static HttpClient CreateClient(CameraEndpoint endpoint, TimeSpan timeout) =>
        CameraHttpClientFactory.Create(endpoint.OnvifServiceUri, credentials: null, timeout);

    private static async Task<string> CallAsync(
        HttpClient client,
        Uri serviceUri,
        string action,
        XElement body,
        CameraCredentials credentials,
        string operation,
        CancellationToken cancellationToken)
    {
        string response;
        try
        {
            response = await OnvifClient.SendAsync(
                client,
                serviceUri,
                action,
                body,
                credentials,
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
                        throw new CameraUnavailableException($"ONVIF is unavailable for the '{operation}' operation.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CameraUnavailableException(
                                $"ONVIF did not respond in time for the '{operation}' operation.",
                exception);
        }

        OnvifClient.EnsureSuccess(response, operation);
        return response;
    }

    private static async Task<Uri> ResolveMediaServiceUriAsync(
        HttpClient client,
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CancellationToken cancellationToken)
    {
        try
        {
            var xml = await CallAsync(
                client,
                endpoint.OnvifServiceUri,
                OnvifSoap.Actions.GetCapabilities,
                OnvifSoap.CreateGetCapabilities(),
                credentials,
                "GetCapabilities",
                cancellationToken);

            return OnvifXml.ParseMediaServiceUri(xml) ?? endpoint.OnvifServiceUri;
        }
        catch (CameraModuleException)
        {
                        // Not all firmware supports GetCapabilities; media requests are tried on the device service.
            return endpoint.OnvifServiceUri;
        }
    }

    private static async Task<string> GetStreamUriAsync(
        HttpClient client,
        Uri mediaServiceUri,
        string profileToken,
        CameraCredentials credentials,
        CancellationToken cancellationToken)
    {
        var xml = await CallAsync(
            client,
            mediaServiceUri,
            OnvifSoap.Actions.GetStreamUri,
            OnvifSoap.CreateGetStreamUri(profileToken),
            credentials,
            "GetStreamUri",
            cancellationToken);

        return OnvifXml.StripCredentials(OnvifXml.ParseUri(xml));
    }

    private static CameraProfile ToCameraProfile(OnvifProfile profile, string rtspUrl) =>
        new(
            Kind: CameraStreamProfileKind.Sub,
            Id: profile.Token,
            Name: profile.Name,
            Video: profile.Video,
            Audio: profile.Audio,
            RtspUrl: rtspUrl);

    private static IReadOnlyList<CameraProfile> AssignKinds(IReadOnlyList<CameraProfile> profiles)
    {
        if (profiles.Count == 1)
        {
            return [profiles[0] with { Kind = CameraStreamProfileKind.Main }];
        }

        var ordered = profiles
            .OrderByDescending(static profile => profile.Video.Width * profile.Video.Height)
            .ToList();

        var result = new List<CameraProfile>(profiles.Count)
        {
            ordered[0] with { Kind = CameraStreamProfileKind.Main },
        };

        result.AddRange(ordered.Skip(1).Select(static profile => profile with { Kind = CameraStreamProfileKind.Sub }));
        return result;
    }

    private static async Task<byte[]> DownloadAsync(
        string url,
        CameraCredentials credentials,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(url, UriKind.Absolute);
        using var client = CameraHttpClientFactory.Create(
            new Uri(uri.GetLeftPart(UriPartial.Authority)),
            credentials,
            TimeSpan.FromSeconds(10));

        using var response = await client.GetAsync(uri, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
                        throw new CameraAuthenticationException("ONVIF rejected the credentials when requesting a snapshot.");
        }

        if (!response.IsSuccessStatusCode)
        {
                        throw new CameraUnavailableException($"ONVIF returned HTTP {(int)response.StatusCode} when requesting a snapshot.");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}
