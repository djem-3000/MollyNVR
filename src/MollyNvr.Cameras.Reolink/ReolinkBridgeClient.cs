using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Cameras.Reolink;

/// <summary>
/// Клиент моста neolink.net: добавляет камеру в его конфигурацию, ждёт подключения,
/// читает профили потоков и забирает стоп-кадры.
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

    /// <summary>Стабильное имя камеры в мосте (используется в RTSP-пути).</summary>
    public static string CameraName(IPAddress address) =>
        $"cam-{address.ToString().Replace('.', '-')}";

    /// <summary>RTSP-адрес потока камеры на мосте.</summary>
    public Uri BuildRtspUrl(string cameraName, string stream) =>
        new(EnsureSlash(options.RtspBaseUrl), $"{cameraName}/{stream}");

    /// <summary>Доступен ли HTTP-API моста (процесс запущен и отвечает).</summary>
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

    /// <summary>Добавляет или обновляет камеру в конфигурации моста (мост перезапустится сам).</summary>
    /// <exception cref="ReolinkBridgeException">Конфигурацию не удалось прочитать или записать.</exception>
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
                $"Не удалось записать конфигурацию моста Reolink ({options.ConfigPath}): {exception.Message}",
                exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new ReolinkBridgeException(exception.Message, exception);
        }
    }

    /// <summary>Ждёт, пока мост подключится к камере (после перезапуска с новой конфигурацией).</summary>
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

    /// <summary>JPEG-стоп-кадр с камеры; <c>null</c>, если мост его не отдал.</summary>
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
