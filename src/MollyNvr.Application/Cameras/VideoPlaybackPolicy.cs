namespace MollyNvr.Application.Cameras;

/// <summary>Политика видеотранскода: H.265 не везде поддерживается браузерами.</summary>
public static class VideoPlaybackPolicy
{
    /// <summary>Целевой кодек перекодирования (принимают все браузеры).</summary>
    public const string TargetCodec = "H264";

    /// <summary>Нужен ли резервный H.264-источник для кодека камеры.</summary>
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

    /// <summary>Строковое значение для API.</summary>
    public static string ToApiValue(string? videoCodec) =>
        NeedsTranscode(videoCodec) ? "transcode-available" : "passthrough";

    /// <summary>Целевой кодек, если транскод доступен.</summary>
    public static string? TargetCodecFor(string? videoCodec) =>
        NeedsTranscode(videoCodec) ? TargetCodec : null;
}
