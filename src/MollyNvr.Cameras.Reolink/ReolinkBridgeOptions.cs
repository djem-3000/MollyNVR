namespace MollyNvr.Cameras.Reolink;

/// <summary>
/// neolink.net bridge parameters: it talks to the camera over Baichuan (port 9000)
/// and exposes regular RTSP plus an HTTP API.
/// </summary>
public sealed record ReolinkBridgeOptions
{
        /// <summary>Base address of the bridge HTTP API (with a trailing slash).</summary>
    public required Uri ApiBaseUrl { get; init; }

        /// <summary>Base RTSP address of the bridge; the camera name and stream are appended to it.</summary>
    public required Uri RtspBaseUrl { get; init; }

        /// <summary>Bridge configuration file; changing it restarts the bridge.</summary>
    public required string ConfigPath { get; init; }

        /// <summary>How long to wait for the camera to appear on the network after the configuration is written.</summary>
    public TimeSpan OnlineTimeout { get; init; } = TimeSpan.FromSeconds(35);
}
