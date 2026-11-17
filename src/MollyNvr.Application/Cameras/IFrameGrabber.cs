namespace MollyNvr.Application.Cameras;

/// <summary>
/// Получение JPEG-кадра из RTSP-источника для камер, у которых нет нативного снапшота
/// (например, HTTP-порт камеры закрыт).
/// </summary>
public interface IFrameGrabber
{
    /// <summary>Возвращает JPEG-кадр источника. Источник содержит учётные данные и не попадает в логи.</summary>
    Task<byte[]> GrabJpegAsync(string sourceUrl, CancellationToken cancellationToken);
}
