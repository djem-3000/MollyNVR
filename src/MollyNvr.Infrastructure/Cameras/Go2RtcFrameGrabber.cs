using System.Net;
using Microsoft.Extensions.Options;
using MollyNvr.Application.Cameras;
using MollyNvr.Cameras.Abstractions;
using MollyNvr.Infrastructure.Media;

namespace MollyNvr.Infrastructure.Cameras;

/// <summary>Получение JPEG-кадра через go2rtc (<c>/api/frame.jpeg</c>): работает для RTSP с любым кодеком.</summary>
public sealed class Go2RtcFrameGrabber(IHttpClientFactory httpClientFactory, IOptions<Go2RtcOptions> options) : IFrameGrabber
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(25);

    /// <inheritdoc />
    public async Task<byte[]> GrabJpegAsync(string sourceUrl, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUrl);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        using var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(options.Value.BaseUrl, UriKind.Absolute);
        client.Timeout = Timeout;

        using var response = await client.GetAsync(
            $"api/frame.jpeg?src={Uri.EscapeDataString(sourceUrl)}",
            timeout.Token);

        if (!response.IsSuccessStatusCode)
        {
            throw new CameraUnavailableException(
                $"Медиа-ядро вернуло HTTP {(int)response.StatusCode} при запросе кадра.");
        }

        var frame = await response.Content.ReadAsByteArrayAsync(timeout.Token);
        if (frame.Length < 2 || frame[0] != 0xFF || frame[1] != 0xD8)
        {
            throw new CameraUnavailableException("Медиа-ядро вернуло не JPEG-кадр.");
        }

        return frame;
    }
}
