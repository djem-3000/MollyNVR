namespace MollyNvr.Application.Cameras;

/// <summary>Как звук профиля доходит до браузера.</summary>
public enum AudioPlaybackMode
{
    /// <summary>Аудио у профиля отключено.</summary>
    Disabled,

    /// <summary>Кодек уже поддерживается WebRTC — без перекодирования.</summary>
    Passthrough,

    /// <summary>Кодек перекодируется сервером в Opus.</summary>
    Transcoded,

    /// <summary>Кодек не поддерживается (нет декодера/WebRTC) — звука не будет.</summary>
    Unsupported,
}

/// <summary>Политика воспроизведения аудио в WebRTC (единая для API и регистрации потоков).</summary>
public static class AudioPlaybackPolicy
{
    /// <summary>Целевой кодек перекодирования.</summary>
    public const string TargetCodec = "Opus";

    /// <summary>Определяет режим по кодеку камеры.</summary>
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

        // FFmpeg не умеет декодировать G.722.1 — перекодирование невозможно.
        if (ContainsAny(codec, "G.722.1", "G7221"))
        {
            return AudioPlaybackMode.Unsupported;
        }

        return AudioPlaybackMode.Transcoded;
    }

    /// <summary>Строковое значение режима для API/UI.</summary>
    public static string ToApiValue(AudioPlaybackMode mode) => mode switch
    {
        AudioPlaybackMode.Disabled => "disabled",
        AudioPlaybackMode.Passthrough => "passthrough",
        AudioPlaybackMode.Transcoded => "transcoded",
        AudioPlaybackMode.Unsupported => "unsupported",
        _ => "unsupported",
    };

    /// <summary>Целевой кодек для режима (только при перекодировании).</summary>
    public static string? TargetCodecFor(AudioPlaybackMode mode) =>
        mode == AudioPlaybackMode.Transcoded ? TargetCodec : null;

    private static bool ContainsAny(string value, params string[] fragments) =>
        fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}
