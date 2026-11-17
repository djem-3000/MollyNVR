using Microsoft.Extensions.Configuration;
using MollyNvr.Cameras.Reolink;
using MollyNvr.Infrastructure.Storage;

namespace MollyNvr.Infrastructure.Cameras;

/// <summary>Настройки моста Reolink: HTTP-API и RTSP-сервер процесса neolink.net.</summary>
public sealed class ReolinkOptions
{
    public const string SectionName = "Reolink";

    /// <summary>Базовый адрес HTTP-API моста (в compose — <c>http://reolink:8655</c>).</summary>
    public string ApiBaseUrl { get; set; } = "http://127.0.0.1:8655";

    /// <summary>Базовый адрес RTSP моста (в compose — <c>rtsp://reolink:8654</c>).</summary>
    public string RtspBaseUrl { get; set; } = "rtsp://127.0.0.1:8654";

    /// <summary>Файл конфигурации моста; по умолчанию <c>&lt;data&gt;/reolink/config.json</c>.</summary>
    public string? ConfigPath { get; set; }

    /// <summary>Сколько ждать подключения камеры после перезапуска моста.</summary>
    public int OnlineTimeoutSeconds { get; set; } = 35;

    public static ReolinkOptions Resolve(IConfiguration configuration)
    {
        var options = new ReolinkOptions();

        if (configuration[$"{SectionName}:ApiBaseUrl"] is { Length: > 0 } apiBaseUrl)
        {
            options.ApiBaseUrl = apiBaseUrl;
        }

        if (configuration[$"{SectionName}:RtspBaseUrl"] is { Length: > 0 } rtspBaseUrl)
        {
            options.RtspBaseUrl = rtspBaseUrl;
        }

        if (configuration[$"{SectionName}:ConfigPath"] is { Length: > 0 } configPath)
        {
            options.ConfigPath = configPath;
        }

        if (int.TryParse(configuration[$"{SectionName}:OnlineTimeoutSeconds"], out var timeout) && timeout > 0)
        {
            options.OnlineTimeoutSeconds = timeout;
        }

        return options;
    }

    public ReolinkBridgeOptions ToBridgeOptions(StorageOptions storage) => new()
    {
        ApiBaseUrl = new Uri(ApiBaseUrl, UriKind.Absolute),
        RtspBaseUrl = new Uri(RtspBaseUrl, UriKind.Absolute),
        ConfigPath = string.IsNullOrWhiteSpace(ConfigPath)
            ? Path.Combine(storage.DataDirectory, "reolink", "config.json")
            : Path.GetFullPath(ConfigPath),
        OnlineTimeout = TimeSpan.FromSeconds(OnlineTimeoutSeconds),
    };
}
