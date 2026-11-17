using System.Net;
using MollyNvr.Cameras.Abstractions;
using MollyNvr.Cameras.Abstractions.Http;

namespace MollyNvr.Cameras.Hikvision;

/// <summary>Модуль камер Hikvision (ISAPI, HTTP Digest).</summary>
public sealed class HikvisionCameraModule : ICameraModule, ICameraNetworkInfoProvider, ICameraEventSource
{
    /// <summary>Ключ модуля.</summary>
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
    /// Признак ISAPI: XML в пространстве имён isapi.org либо Digest-запрос с Hikvision-областью.
    /// Ответ 200/401 от чужого веб-интерфейса (роутеры, веб-панели) признаком не является.
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
            "сетевые параметры",
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

        var deviceXml = await GetStringAsync(client, "/ISAPI/System/deviceInfo", "сведения об устройстве", cancellationToken);
        var channelsXml = await GetStringAsync(client, "/ISAPI/Streaming/channels", "список каналов", cancellationToken);

        var device = HikvisionIsapiXml.ParseDeviceInfo(deviceXml);
        var profiles = HikvisionIsapiXml.ParseProfiles(channelsXml, endpoint);

        if (profiles.Count == 0)
        {
            throw new CameraOperationNotSupportedException("Hikvision не вернул ни одного активного видеоканала.");
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
            throw new CameraAuthenticationException("Hikvision отклонил учётные данные.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new CameraUnavailableException($"Hikvision вернул HTTP {(int)response.StatusCode} при запросе снапшота.");
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
            throw new CameraAuthenticationException("Hikvision отклонил учётные данные.");
        }

        var status = HikvisionIsapiXml.ReadErrorStatus(body);
        var details = status is null ? string.Empty : $": {status}";

        throw new CameraUnavailableException(
            $"Hikvision вернул HTTP {(int)response.StatusCode} при операции «{action}»{details}.");
    }
}
