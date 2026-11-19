namespace MollyNvr.Application.Cameras;

/// <summary>Video transcode policy: H.265 is not supported by every browser.</summary>
public static class VideoPlaybackPolicy
{
        /// <summary>Target transcoding codec (accepted by all browsers).</summary>
    public const string TargetCodec = "H264";

        /// <summary>Whether a fallback H.264 source is needed for the camera codec.</summary>
    public static bool NeedsTranscode(string? videoCodec)
    {
        if (string.IsNullOrWhiteSpace(videoCodec))
        {
            return false;
        }

        var normalized = videoCodec
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

        return normalized is "H265" or "HEVC";
    }

        /// <summary>String value for the API.</summary>
    public static string ToApiValue(string? videoCodec) =>
        NeedsTranscode(videoCodec) ? "transcode-available" : "passthrough";

        /// <summary>Target codec when transcoding is available.</summary>
    public static string? TargetCodecFor(string? videoCodec) =>
        NeedsTranscode(videoCodec) ? TargetCodec : null;
}
