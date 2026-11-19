namespace MollyNvr.Cameras.Abstractions;

/// <summary>Normalization of video codec names to a single form (H.264, H.265, MJPEG).</summary>
public static class VideoCodecNames
{
    public const string H264 = "H.264";
    public const string H265 = "H.265";
    public const string Mjpeg = "MJPEG";
    public const string Unknown = "Unknown";

        /// <summary>Normalizes a codec name from ISAPI/ONVIF/SDP to a single form.</summary>
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

/// <summary>Normalization of audio codec names.</summary>
public static class AudioCodecNames
{
    public const string Aac = "AAC";
    public const string G711Ulaw = "G.711ulaw";
    public const string G711Alaw = "G.711alaw";
    public const string Opus = "Opus";

        /// <summary>Normalizes an audio codec name from ISAPI/ONVIF/SDP to a single form.</summary>
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

        /// <summary>Sample rate when it is uniquely determined by the codec (G.711 — 8 kHz).</summary>
    public static int? KnownSampleRate(string? codec) => Normalize(codec) switch
    {
        G711Ulaw or G711Alaw => 8000,
        _ => null,
    };
}
