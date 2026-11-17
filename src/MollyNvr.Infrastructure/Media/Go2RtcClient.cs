using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MollyNvr.Application.Streams;
using MollyNvr.Domain.Streams;

namespace MollyNvr.Infrastructure.Media;

/// <summary>Клиент HTTP API go2rtc (порт IMediaEngine).</summary>
public sealed class Go2RtcClient(HttpClient httpClient, ILogger<Go2RtcClient> logger) : IMediaEngine
{
    public async Task<IReadOnlyList<string>> GetStreamIdsAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("api/streams", cancellationToken);
        EnsureSuccess(response, "получить список потоков");

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().Select(static property => property.Name).ToList();
    }

    public async Task UpsertStreamAsync(
        StreamId id,
        IReadOnlyList<string> sourceUrls,
        CancellationToken cancellationToken)
    {
        if (sourceUrls.Count == 0)
        {
            throw new ArgumentException("Нужен хотя бы один источник потока.", nameof(sourceUrls));
        }

        var requestUri = new StringBuilder($"api/streams?name={Uri.EscapeDataString(id.Value)}");

        foreach (var sourceUrl in sourceUrls)
        {
            requestUri.Append("&src=").Append(Uri.EscapeDataString(sourceUrl));
        }

        using var response = await httpClient.PutAsync(requestUri.ToString(), content: null, cancellationToken);
        EnsureSuccess(response, "добавить источник");
    }

    public async Task RemoveStreamAsync(StreamId id, CancellationToken cancellationToken)
    {
        using var response = await httpClient.DeleteAsync($"api/streams?src={Uri.EscapeDataString(id.Value)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        EnsureSuccess(response, "удалить источник");
    }

    public async Task<string> NegotiateWebRtcAsync(StreamId id, string sdpOffer, CancellationToken cancellationToken)
    {
        using var content = new StringContent(sdpOffer, Encoding.UTF8, "application/sdp");
        using var response = await httpClient.PostAsync($"api/webrtc?src={Uri.EscapeDataString(id.Value)}", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("go2rtc отклонил WebRTC-запрос потока {StreamId}: HTTP {StatusCode}", id.Value, (int)response.StatusCode);
            throw new MediaEngineException($"Медиа-ядро отклонило WebRTC-запрос (HTTP {(int)response.StatusCode}).");
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private void EnsureSuccess(HttpResponseMessage response, string action)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        logger.LogWarning("Не удалось {Action} в go2rtc: HTTP {StatusCode}", action, (int)response.StatusCode);
        throw new MediaEngineException($"Медиа-ядро вернуло HTTP {(int)response.StatusCode} при попытке {action}.");
    }
}
