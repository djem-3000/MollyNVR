using MollyNvr.Domain.Streams;

namespace MollyNvr.Application.Tests.Fakes;

internal sealed class FakeMediaEngine : IMediaEngine
{
    public List<(string Id, IReadOnlyList<string> Urls)> Upserts { get; } = [];

    public List<string> Removals { get; } = [];

    public List<string> Negotiations { get; } = [];

    public string AnswerSdp { get; set; } = "v=0\r\n";

    public Exception? FailOnUpsert { get; set; }

    public Task<IReadOnlyList<string>> GetStreamIdsAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<string>>(Upserts.Select(static upsert => upsert.Id).ToList());

    public Task UpsertStreamAsync(StreamId id, IReadOnlyList<string> sourceUrls, CancellationToken cancellationToken)
    {
        if (FailOnUpsert is not null)
        {
            return Task.FromException(FailOnUpsert);
        }

        Upserts.Add((id.Value, sourceUrls));
        return Task.CompletedTask;
    }

    public Task RemoveStreamAsync(StreamId id, CancellationToken cancellationToken)
    {
        Removals.Add(id.Value);
        return Task.CompletedTask;
    }

    public Task<string> NegotiateWebRtcAsync(StreamId id, string sdpOffer, CancellationToken cancellationToken)
    {
        Negotiations.Add(id.Value);
        return Task.FromResult(AnswerSdp);
    }
}
