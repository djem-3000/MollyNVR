using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Cameras.Reolink;

/// <summary>
/// neolink.net bridge client: adds a camera to its configuration, waits for the connection,
/// reads stream profiles and fetches snapshots.
/// </summary>
public sealed class ReolinkBridgeClient(HttpClient httpClient, ReolinkBridgeOptions options)
{
    private const string CamerasPath = "api/cameras";
    private const string SnapshotFile = "snapshot.jpg";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1.5);

        /// <summary>Stable camera name in the bridge (used in the RTSP path).</summary>
    public static string CameraName(IPAddress address) =>
        $"cam-{address.ToString().Replace('.', '-')}";

        /// <summary>RTSP address of the camera stream on the bridge.</summary>
    public Uri BuildRtspUrl(string cameraName, string stream) =>
        new(EnsureSlash(options.RtspBaseUrl), $"{cameraName}/{stream}");

        /// <summary>Whether the bridge HTTP API is available (the process is running and responds).</summary>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(Url(CamerasPath), cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

        /// <summary>Adds or updates the camera in the bridge configuration (the bridge restarts itself).</summary>
        /// <exception cref="ReolinkBridgeException">The configuration could not be read or written.</exception>
    public async Task ProvisionAsync(
        IPAddress address,
        CameraCredentials credentials,
        CancellationToken cancellationToken)
    {
        try
        {
            await ReolinkConfigStore.UpsertAsync(
                options.ConfigPath,
                CameraName(address),
                address.ToString(),
                credentials,
                cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ReolinkBridgeException(
                                $"Failed to write the Reolink bridge configuration ({options.ConfigPath}): {exception.Message}",
                exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new ReolinkBridgeException(exception.Message, exception);
        }
    }

        /// <summary>Waits until the bridge connects to the camera (after a restart with the new configuration).</summary>
    public async Task<bool> WaitUntilOnlineAsync(string cameraName, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + options.OnlineTimeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            var cameras = await TryGetCamerasAsync(cancellationToken);
            var camera = cameras?.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, cameraName, StringComparison.OrdinalIgnoreCase));

            if (camera?.Online == true)
            {
                return true;
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        return false;
    }

    internal async Task<BridgeStreamInfo?> GetStreamInfoAsync(
        string cameraName,
        CancellationToken cancellationToken) =>
        await GetJsonAsync<BridgeStreamInfo>(
            $"{CamerasPath}/{Uri.EscapeDataString(cameraName)}/streaminfo",
            cancellationToken);

    internal async Task<BridgeVersion?> GetDeviceInfoAsync(
        string cameraName,
        CancellationToken cancellationToken)
    {
        var capabilities = await GetJsonAsync<BridgeCapabilities>(
            $"{CamerasPath}/{Uri.EscapeDataString(cameraName)}/capabilities",
            cancellationToken);

        return capabilities?.Version;
    }

        /// <summary>JPEG snapshot from the camera; <c>null</c> when the bridge did not provide one.</summary>
    public async Task<byte[]?> GetSnapshotAsync(string cameraName, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                Url($"{CamerasPath}/{Uri.EscapeDataString(cameraName)}/{SnapshotFile}"),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<BridgeCamera>?> TryGetCamerasAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(Url(CamerasPath), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<List<BridgeCamera>>(JsonOptions, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<T?> GetJsonAsync<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var response = await httpClient.GetAsync(Url(path), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private Uri Url(string path) => new(EnsureSlash(options.ApiBaseUrl), path);

    private static Uri EnsureSlash(Uri uri) =>
        uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri.AbsoluteUri + "/");
}
