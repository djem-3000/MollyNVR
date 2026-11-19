namespace MollyNvr.Application.Cameras;

/// <summary>Typical camera ports and their mapping to default protocols.</summary>
public static class CameraPortCatalog
{
    private const int HttpPort = 80;
    private const int HttpsPort = 443;
    private const int SdkPort = 8000;
    private const int RtspPort = 554;
    private const int RtspAltPort = 8554;

        /// <summary>Ports checked during a device probe.</summary>
    public static IReadOnlyList<int> ScanPorts { get; } =
    [
        HttpPort,
        HttpsPort,
        RtspPort,
        1935,
        2020,
        3000,
        5000,
        SdkPort,
        8001,
        8080,
        8081,
        8443,
        RtspAltPort,
        8899,
        9000,
        34567,
        37777,
    ];

    /// <summary>
        /// Fast port set for host discovery in a subnet: only the likely camera video services.
        /// The full <see cref="ScanPorts"/> is then checked only on the discovered hosts.
        /// HTTP ports are not needed here: a host with only a web interface is not considered a camera.
    /// </summary>
    public static IReadOnlyList<int> DiscoveryPorts { get; } =
    [
        RtspPort,
        RtspAltPort,
        SdkPort,
        9000,
        34567,
        37777,
    ];

        /// <summary>Protocols running on the default port for the specified vendor.</summary>
    public static IReadOnlyList<CameraProtocolMatch> Match(string vendorKey, int port)
    {
        var matches = new List<CameraProtocolMatch>();

        if (port == HttpPort)
        {
            matches.Add(new("HTTP", "control"));
        }

        if (port == HttpsPort)
        {
            matches.Add(new("HTTPS", "control"));
        }

        if (port is RtspPort or RtspAltPort)
        {
            matches.Add(new("RTSP", "video"));
        }

        switch (NormalizeVendorKey(vendorKey))
        {
            case "hikvision":
                if (port == HttpPort)
                {
                    matches.Add(new("ISAPI", "control"));
                }

                if (port == SdkPort)
                {
                    matches.Add(new("SDK", "control"));
                }

                break;

            case "onvif":
                if (port == SdkPort)
                {
                    matches.Add(new("ONVIF", "control"));
                }

                break;
        }

        return matches;
    }

    private static string NormalizeVendorKey(string vendorKey) =>
        string.Equals(vendorKey, "reolink", StringComparison.OrdinalIgnoreCase)
            ? "onvif"
            : vendorKey.ToLowerInvariant();
}

/// <summary>Protocol running on the default port.</summary>
public sealed record CameraProtocolMatch(string Name, string Role);
