using Microsoft.Extensions.Configuration;
using MollyNvr.Cameras.Reolink;
using MollyNvr.Infrastructure.Storage;

namespace MollyNvr.Infrastructure.Cameras;

/// <summary>Reolink bridge settings: HTTP API and RTSP server of the neolink.net process.</summary>
public sealed class ReolinkOptions
{
    public const string SectionName = "Reolink";

        /// <summary>Base address of the bridge HTTP API (in compose — <c>http://reolink:8655</c>).</summary>
    public string ApiBaseUrl { get; set; } = "http://127.0.0.1:8655";

        /// <summary>Base RTSP address of the bridge (in compose — <c>rtsp://reolink:8654</c>).</summary>
    public string RtspBaseUrl { get; set; } = "rtsp://127.0.0.1:8654";

        /// <summary>Bridge configuration file; <c>&lt;data&gt;/reolink/config.json</c> by default.</summary>
    public string? ConfigPath { get; set; }

        /// <summary>How long to wait for the camera connection after the bridge restarts.</summary>
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
