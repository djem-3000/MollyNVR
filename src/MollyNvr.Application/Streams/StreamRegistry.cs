using MollyNvr.Domain.Streams;

namespace MollyNvr.Application.Streams;

/// <summary>Stream registry (M1: manual sources stored in process memory).</summary>
public sealed class StreamRegistry(IMediaEngine mediaEngine)
{
    private readonly Dictionary<string, string> _sources = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<StreamInfo> List()
    {
        lock (_sources)
        {
            return _sources
                .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(static pair => new StreamInfo(pair.Key, StreamUrlMasker.Mask(pair.Value)))
                .ToList();
        }
    }

    public async Task<StreamInfo> AddAsync(string id, string? sourceUrl, CancellationToken cancellationToken)
    {
        var streamId = StreamId.Create(id);
        var url = ValidateSource(sourceUrl);

        lock (_sources)
        {
            if (!_sources.TryAdd(streamId.Value, url))
            {
                throw new StreamAlreadyExistsException(streamId.Value);
            }
        }

        try
        {
            await mediaEngine.UpsertStreamAsync(streamId, [url], cancellationToken);
        }
        catch
        {
            lock (_sources)
            {
                _sources.Remove(streamId.Value);
            }

            throw;
        }

        return new StreamInfo(streamId.Value, StreamUrlMasker.Mask(url));
    }

    public async Task RemoveAsync(string id, CancellationToken cancellationToken)
    {
        var streamId = StreamId.Create(id);

        lock (_sources)
        {
            if (!_sources.Remove(streamId.Value))
            {
                throw new StreamNotFoundException(streamId.Value);
            }
        }

        await mediaEngine.RemoveStreamAsync(streamId, cancellationToken);
    }

    public async Task<string> NegotiateAsync(string id, string sdpOffer, CancellationToken cancellationToken)
    {
        var streamId = StreamId.Create(id);

        lock (_sources)
        {
            if (!_sources.ContainsKey(streamId.Value))
            {
                throw new StreamNotFoundException(streamId.Value);
            }
        }

        return await mediaEngine.NegotiateWebRtcAsync(streamId, sdpOffer, cancellationToken);
    }

        /// <summary>Re-applies the sources in the media engine (after it restarts).</summary>
    public async Task SyncAsync(CancellationToken cancellationToken)
    {
        List<KeyValuePair<string, string>> snapshot;
        lock (_sources)
        {
            snapshot = _sources.ToList();
        }

        foreach (var (id, url) in snapshot)
        {
            await mediaEngine.UpsertStreamAsync(StreamId.Create(id), [url], cancellationToken);
        }
    }

    private static string ValidateSource(string? sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
                        throw new ArgumentException("The source URL cannot be empty.", nameof(sourceUrl));
        }

        var trimmed = sourceUrl.Trim();

                // ffmpeg: sources in go2rtc are for test and virtual streams.
        if (trimmed.StartsWith("ffmpeg:", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "rtsp" && uri.Scheme != "rtsps"))
        {
                        throw new ArgumentException("A stream URL like rtsp://... or rtsps://... is expected.", nameof(sourceUrl));
        }

        return trimmed;
    }
}
