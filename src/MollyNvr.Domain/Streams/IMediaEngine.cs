namespace MollyNvr.Domain.Streams;

/// <summary>Media engine port: source management and WebRTC negotiation (WHEP).</summary>
public interface IMediaEngine
{
    Task<IReadOnlyList<string>> GetStreamIdsAsync(CancellationToken cancellationToken);

    Task UpsertStreamAsync(StreamId id, IReadOnlyList<string> sourceUrls, CancellationToken cancellationToken);

    Task RemoveStreamAsync(StreamId id, CancellationToken cancellationToken);

    Task<string> NegotiateWebRtcAsync(StreamId id, string sdpOffer, CancellationToken cancellationToken);
}
