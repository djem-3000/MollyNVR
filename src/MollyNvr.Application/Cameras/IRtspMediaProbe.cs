using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Application.Cameras;

/// <summary>Media stream information obtained from SDP (RTSP DESCRIBE).</summary>
public sealed record RtspMediaInfo(string? VideoCodec, string? AudioCodec, int? AudioSampleRate);

/// <summary>Detection of the actual RTSP stream codecs (SDP).</summary>
public interface IRtspMediaProbe
{
    /// <summary>
        /// Requests the stream SDP. Returns <c>null</c> when the stream is unavailable or the response cannot be parsed.
    /// </summary>
    Task<RtspMediaInfo?> ProbeAsync(Uri rtspUri, CameraCredentials credentials, CancellationToken cancellationToken);
}
