namespace MollyNvr.Infrastructure.Media;

public sealed class Go2RtcOptions
{
    public const string SectionName = "Go2Rtc";

    /// <summary>Базовый адрес HTTP API go2rtc.</summary>
    public string BaseUrl { get; set; } = "http://127.0.0.1:1984";

    /// <summary>Путь к бинарнику go2rtc; по умолчанию ищется tools/go2rtc(.exe), затем /usr/local/bin/go2rtc.</summary>
    public string? BinaryPath { get; set; }

    /// <summary>Путь к конфигу go2rtc; по умолчанию &lt;data&gt;/go2rtc.yaml.</summary>
    public string? ConfigPath { get; set; }

    /// <summary>Адрес прослушивания WebRTC (медиапоток для браузеров).</summary>
    public string WebRtcListen { get; set; } = ":8555";

    /// <summary>
    /// Адрес внутреннего RTSP-сервера go2rtc. Нужен для ffmpeg-источников и транскода:
    /// go2rtc пишет результат ffmpeg в rtsp://localhost:&lt;port&gt;/&lt;id&gt;. Только loopback.
    /// </summary>
    public string RtspListen { get; set; } = "127.0.0.1:8554";

    /// <summary>Явные ICE-кандидаты WebRTC (например, LAN-адрес хоста); пусто — автоопределение go2rtc.</summary>
    public string[] WebRtcCandidates { get; set; } = [];
}
