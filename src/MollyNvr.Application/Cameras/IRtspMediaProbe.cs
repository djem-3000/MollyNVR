using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Application.Cameras;

/// <summary>Информация о медиапотоке, полученная из SDP (RTSP DESCRIBE).</summary>
public sealed record RtspMediaInfo(string? VideoCodec, string? AudioCodec, int? AudioSampleRate);

/// <summary>Определение фактических кодеков RTSP-потока (SDP).</summary>
public interface IRtspMediaProbe
{
    /// <summary>
    /// Запрашивает SDP потока. Возвращает <c>null</c>, если поток недоступен или ответ не разобран.
    /// </summary>
    Task<RtspMediaInfo?> ProbeAsync(Uri rtspUri, CameraCredentials credentials, CancellationToken cancellationToken);
}
