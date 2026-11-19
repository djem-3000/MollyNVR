namespace MollyNvr.Application.Cameras;

/// <summary>How the profile audio reaches the browser.</summary>
public enum AudioPlaybackMode
{
        /// <summary>Audio is disabled for the profile.</summary>
    Disabled,

        /// <summary>The codec is already supported by WebRTC — no transcoding.</summary>
    Passthrough,

        /// <summary>The codec is transcoded by the server to Opus.</summary>
    Transcoded,

        /// <summary>The codec is not supported (no decoder/WebRTC) — there will be no sound.</summary>
    Unsupported,
}

/// <summary>WebRTC audio playback policy (shared by the API and stream registration).</summary>
public static class AudioPlaybackPolicy
{
        /// <summary>Target transcoding codec.</summary>
    public const string TargetCodec = "Opus";

        /// <summary>Determines the mode from the camera codec.</summary>
    public static AudioPlaybackMode Classify(string? audioCodec)
    {
        if (string.IsNullOrWhiteSpace(audioCodec))
        {
            return AudioPlaybackMode.Transcoded;
        }

        var codec = audioCodec.Trim();

        if (ContainsAny(codec, "PCMU", "PCMA", "OPUS", "G.711", "G711"))
        {
            return AudioPlaybackMode.Passthrough;
        }

                // FFmpeg cannot decode G.722.1 — transcoding is impossible.
        if (ContainsAny(codec, "G.722.1", "G7221"))
        {
            return AudioPlaybackMode.Unsupported;
        }

        return AudioPlaybackMode.Transcoded;
    }

        /// <summary>String value of the mode for the API and UI.</summary>
    public static string ToApiValue(AudioPlaybackMode mode) => mode switch
    {
        AudioPlaybackMode.Disabled => "disabled",
        AudioPlaybackMode.Passthrough => "passthrough",
        AudioPlaybackMode.Transcoded => "transcoded",
        AudioPlaybackMode.Unsupported => "unsupported",
        _ => "unsupported",
    };

        /// <summary>Target codec for the mode (only when transcoding).</summary>
    public static string? TargetCodecFor(AudioPlaybackMode mode) =>
        mode == AudioPlaybackMode.Transcoded ? TargetCodec : null;

    private static bool ContainsAny(string value, params string[] fragments) =>
        fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}
