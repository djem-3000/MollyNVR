using System.Net;
using MollyNvr.Cameras.Abstractions;
using MollyNvr.Cameras.Abstractions.Http;

namespace MollyNvr.Cameras.Hikvision;

/// <summary>Hikvision camera module (ISAPI, HTTP Digest).</summary>
public sealed class HikvisionCameraModule : ICameraModule, ICameraNetworkInfoProvider, ICameraEventSource
{
        /// <summary>Module key.</summary>
    public const string ModuleKey = "hikvision";

    /// <inheritdoc />
    public Task<IReadOnlyList<CameraEventType>> GetEventTypesAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CancellationToken cancellationToken) =>
        HikvisionEvents.GetEventTypesAsync(endpoint, credentials, cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<CameraEvent> WatchEventsAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CancellationToken cancellationToken) =>
        HikvisionEvents.WatchAsync(endpoint, credentials, cancellationToken);

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan QuickTimeout = TimeSpan.FromSeconds(3);

    /// <inheritdoc />
    public string Key => ModuleKey;

    /// <inheritdoc />
    public string DisplayName => "Hikvision";

    /// <inheritdoc />
    public async Task<bool> CanHandleAsync(CameraEndpoint endpoint, CancellationToken cancellationToken)
    {
        using var client = CreateClient(endpoint, credentials: null, QuickTimeout);

        try
        {
            using var response = await client.GetAsync("/ISAPI/System/deviceInfo", cancellationToken);
            if (response.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.OK))
            {
                return false;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return IsIsapiResponse(response, body);
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

    /// <summary>
        /// ISAPI marker: XML in the isapi.org namespace or a Digest request with a Hikvision realm.
        /// A 200/401 response from a third-party web interface (routers, web panels) is not such a marker.
    /// </summary>
    internal static bool IsIsapiResponse(HttpResponseMessage response, string body)
    {
        if (body.Contains("isapi.org", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType?.Contains("xml", StringComparison.OrdinalIgnoreCase) != true)
        {
            return false;
        }

        var challenge = string.Join(' ', response.Headers.WwwAuthenticate.Select(static header => header.ToString()));

        return challenge.Contains("DS-", StringComparison.OrdinalIgnoreCase) ||
               challenge.Contains("Hikvision", StringComparison.OrdinalIgnoreCase) ||
               challenge.Contains("IP Camera", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<bool?> GetDhcpEnabledAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(endpoint, credentials, ProbeTimeout);

        var xml = await GetStringAsync(
            client,
            "/ISAPI/System/Network/interfaces",
                        "network parameters",
            cancellationToken);

        return HikvisionIsapiXml.ParseDhcpEnabled(xml);
    }

    /// <inheritdoc />
    public async Task<CameraProbeResult> ProbeAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(endpoint, credentials, ProbeTimeout);

                var deviceXml = await GetStringAsync(client, "/ISAPI/System/deviceInfo", "device details", cancellationToken);
                var channelsXml = await GetStringAsync(client, "/ISAPI/Streaming/channels", "channel list", cancellationToken);

        var device = HikvisionIsapiXml.ParseDeviceInfo(deviceXml);
        var profiles = HikvisionIsapiXml.ParseProfiles(channelsXml, endpoint);

        if (profiles.Count == 0)
        {
                        throw new CameraOperationNotSupportedException("Hikvision returned no active video channel.");
        }

        return new CameraProbeResult(device, profiles);
    }

    /// <inheritdoc />
    public async Task<byte[]> GetSnapshotAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CameraProfile profile,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(endpoint, credentials, ProbeTimeout);

        return await GetBytesAsync(client, $"/ISAPI/Streaming/channels/{profile.Id}/picture", cancellationToken);
    }

    private static HttpClient CreateClient(CameraEndpoint endpoint, CameraCredentials? credentials, TimeSpan timeout) =>
        CameraHttpClientFactory.Create(endpoint.BuildHttpUri("/"), credentials, timeout);

    private static async Task<string> GetStringAsync(
        HttpClient client,
        string path,
        string action,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(path, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, body, action);

        return body;
    }

    private static async Task<byte[]> GetBytesAsync(HttpClient client, string path, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(path, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
                        throw new CameraAuthenticationException("Hikvision rejected the credentials.");
        }

        if (!response.IsSuccessStatusCode)
        {
                        throw new CameraUnavailableException($"Hikvision returned HTTP {(int)response.StatusCode} when requesting a snapshot.");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private static void EnsureSuccess(HttpResponseMessage response, string body, string action)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
                        throw new CameraAuthenticationException("Hikvision rejected the credentials.");
        }

        var status = HikvisionIsapiXml.ReadErrorStatus(body);
        var details = status is null ? string.Empty : $": {status}";

        throw new CameraUnavailableException(
                        $"Hikvision returned HTTP {(int)response.StatusCode} for the '{action}' operation{details}.");
    }
}
