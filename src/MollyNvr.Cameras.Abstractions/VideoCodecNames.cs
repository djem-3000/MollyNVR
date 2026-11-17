namespace MollyNvr.Cameras.Abstractions;

/// <summary>Нормализация названий видеокодеков к единому виду (H.264, H.265, MJPEG).</summary>
public static class VideoCodecNames
{
    public const string H264 = "H.264";
    public const string H265 = "H.265";
    public const string Mjpeg = "MJPEG";
    public const string Unknown = "Unknown";

    /// <summary>Приводит название кодека из ISAPI/ONVIF/SDP к единому виду.</summary>
    public static string Normalize(string? codec)
    {
        if (string.IsNullOrWhiteSpace(codec))
        {
            return Unknown;
        }

        var value = codec.Trim();
        if (value.Contains("265", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("hevc", StringComparison.OrdinalIgnoreCase))
        {
            return H265;
        }

        if (value.Contains("264", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("avc", StringComparison.OrdinalIgnoreCase))
        {
            return H264;
        }

        if (value.Contains("mjpeg", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("mjpg", StringComparison.OrdinalIgnoreCase))
        {
            return Mjpeg;
        }

        return value;
    }
}

/// <summary>Нормализация названий аудиокодеков.</summary>
public static class AudioCodecNames
{
    public const string Aac = "AAC";
    public const string G711Ulaw = "G.711ulaw";
    public const string G711Alaw = "G.711alaw";
    public const string Opus = "Opus";

    /// <summary>Приводит название аудиокодека из ISAPI/ONVIF/SDP к единому виду.</summary>
    public static string? Normalize(string? codec)
    {
        if (string.IsNullOrWhiteSpace(codec))
        {
            return null;
        }

        var value = codec.Trim();
        if (value.Equals("PCMU", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("ulaw", StringComparison.OrdinalIgnoreCase))
        {
            return G711Ulaw;
        }

        if (value.Equals("PCMA", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("alaw", StringComparison.OrdinalIgnoreCase))
        {
            return G711Alaw;
        }

        if (value.Contains("aac", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("mpeg4-generic", StringComparison.OrdinalIgnoreCase))
        {
            return Aac;
        }

        if (value.Contains("opus", StringComparison.OrdinalIgnoreCase))
        {
            return Opus;
        }

        return value;
    }

    /// <summary>Частота дискретизации, если она однозначно определена кодеком (G.711 — 8 кГц).</summary>
    public static int? KnownSampleRate(string? codec) => Normalize(codec) switch
    {
        G711Ulaw or G711Alaw => 8000,
        _ => null,
    };
}
