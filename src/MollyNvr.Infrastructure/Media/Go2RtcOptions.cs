namespace MollyNvr.Infrastructure.Media;

public sealed class Go2RtcOptions
{
    public const string SectionName = "Go2Rtc";

        /// <summary>Base address of the go2rtc HTTP API.</summary>
    public string BaseUrl { get; set; } = "http://127.0.0.1:1984";

        /// <summary>Path to the go2rtc binary; by default tools/go2rtc(.exe) is looked up, then /usr/local/bin/go2rtc.</summary>
    public string? BinaryPath { get; set; }

        /// <summary>Path to the go2rtc config; <c>&lt;data&gt;/go2rtc.yaml</c> by default.</summary>
    public string? ConfigPath { get; set; }

        /// <summary>WebRTC listen address (media stream for browsers).</summary>
    public string WebRtcListen { get; set; } = ":8555";

    /// <summary>
        /// Address of the internal go2rtc RTSP server. Needed for ffmpeg sources and transcoding:
        /// go2rtc writes the ffmpeg result to rtsp://localhost:&lt;port&gt;/&lt;id&gt;. Loopback only.
    /// </summary>
    public string RtspListen { get; set; } = "127.0.0.1:8554";

        /// <summary>Explicit WebRTC ICE candidates (for example, the host LAN address); empty — go2rtc autodetection.</summary>
    public string[] WebRtcCandidates { get; set; } = [];
}
