namespace MollyNvr.Application.Cameras;

/// <summary>Типовые порты камер и их сопоставление с протоколами по умолчанию.</summary>
public static class CameraPortCatalog
{
    private const int HttpPort = 80;
    private const int HttpsPort = 443;
    private const int SdkPort = 8000;
    private const int RtspPort = 554;
    private const int RtspAltPort = 8554;

    /// <summary>Порты, которые проверяются при опросе устройства.</summary>
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
    /// Быстрый набор портов для поиска хостов в подсети: только вероятные видеосервисы камер.
    /// Полный <see cref="ScanPorts"/> проверяется затем только у найденных хостов.
    /// HTTP-порты здесь не нужны: хост только с веб-интерфейсом камерой не считается.
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

    /// <summary>Протоколы, работающие на порту по умолчанию для указанного вендора.</summary>
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

/// <summary>Протокол, работающий на порту по умолчанию.</summary>
public sealed record CameraProtocolMatch(string Name, string Role);
