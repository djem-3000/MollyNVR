using MollyNvr.Application.Streams;
using MollyNvr.Application.Tests.Fakes;

namespace MollyNvr.Application.Tests;

public class StreamRegistryTests
{
    private const string SourceUrl = "rtsp://user:secret@192.168.1.10:554/Streaming/Channels/101";
    private const string MaskedUrl = "rtsp://user:***@192.168.1.10:554/Streaming/Channels/101";

    [Fact]
    public async Task AddAsync_RegistersSourceInEngineAndMasksUrl()
    {
        var engine = new FakeMediaEngine();
        var registry = new StreamRegistry(engine);

        var info = await registry.AddAsync("cam1", SourceUrl, CancellationToken.None);

        Assert.Equal("cam1", info.Id);
        Assert.Equal(MaskedUrl, info.Url);
        var upsert = Assert.Single(engine.Upserts);
        Assert.Equal("cam1", upsert.Id);
        Assert.Equal([SourceUrl], upsert.Urls);
    }

    [Fact]
    public async Task AddAsync_DuplicateId_Throws()
    {
        var registry = new StreamRegistry(new FakeMediaEngine());
        await registry.AddAsync("cam1", SourceUrl, CancellationToken.None);

        await Assert.ThrowsAsync<StreamAlreadyExistsException>(
            () => registry.AddAsync("CAM1", SourceUrl, CancellationToken.None));
    }

    [Theory]
    [InlineData("cam 1", SourceUrl)]
    [InlineData("cam1", "")]
    [InlineData("cam1", "http://192.168.1.10/stream")]
    [InlineData("cam1", "не-url")]
    public async Task AddAsync_InvalidInput_Throws(string id, string url)
    {
        var registry = new StreamRegistry(new FakeMediaEngine());

        await Assert.ThrowsAsync<ArgumentException>(() => registry.AddAsync(id, url, CancellationToken.None));
    }

    [Fact]
    public async Task AddAsync_AllowsFfmpegSources()
    {
        var engine = new FakeMediaEngine();
        var registry = new StreamRegistry(engine);

        await registry.AddAsync("test", "ffmpeg:virtual?video#video=h264", CancellationToken.None);

        var upsert = Assert.Single(engine.Upserts);
        Assert.Equal("test", upsert.Id);
        Assert.Equal(["ffmpeg:virtual?video#video=h264"], upsert.Urls);
    }

    [Fact]
    public async Task AddAsync_EngineFailure_DoesNotKeepStream()
    {
        var engine = new FakeMediaEngine { FailOnUpsert = new MediaEngineException("сбой") };
        var registry = new StreamRegistry(engine);

        await Assert.ThrowsAsync<MediaEngineException>(() => registry.AddAsync("cam1", SourceUrl, CancellationToken.None));

        Assert.Empty(registry.List());
        Assert.Empty(engine.Upserts);
    }

    [Fact]
    public async Task RemoveAsync_RemovesFromRegistryAndEngine()
    {
        var engine = new FakeMediaEngine();
        var registry = new StreamRegistry(engine);
        await registry.AddAsync("cam1", SourceUrl, CancellationToken.None);

        await registry.RemoveAsync("cam1", CancellationToken.None);

        Assert.Empty(registry.List());
        Assert.Equal("cam1", Assert.Single(engine.Removals));
    }

    [Fact]
    public async Task RemoveAsync_UnknownStream_Throws()
    {
        var registry = new StreamRegistry(new FakeMediaEngine());

        await Assert.ThrowsAsync<StreamNotFoundException>(() => registry.RemoveAsync("cam1", CancellationToken.None));
    }

    [Fact]
    public async Task NegotiateAsync_KnownStream_ReturnsAnswer()
    {
        var engine = new FakeMediaEngine { AnswerSdp = "v=0 answer" };
        var registry = new StreamRegistry(engine);
        await registry.AddAsync("cam1", SourceUrl, CancellationToken.None);

        var answer = await registry.NegotiateAsync("cam1", "v=0 offer", CancellationToken.None);

        Assert.Equal("v=0 answer", answer);
        Assert.Equal("cam1", Assert.Single(engine.Negotiations));
    }

    [Fact]
    public async Task NegotiateAsync_UnknownStream_Throws()
    {
        var registry = new StreamRegistry(new FakeMediaEngine());

        await Assert.ThrowsAsync<StreamNotFoundException>(
            () => registry.NegotiateAsync("cam1", "v=0 offer", CancellationToken.None));
    }

    [Fact]
    public async Task SyncAsync_ReappliesAllSources()
    {
        var engine = new FakeMediaEngine();
        var registry = new StreamRegistry(engine);
        await registry.AddAsync("cam1", SourceUrl, CancellationToken.None);
        await registry.AddAsync("cam2", "rtsp://192.168.1.11:554/sub", CancellationToken.None);
        engine.Upserts.Clear();

        await registry.SyncAsync(CancellationToken.None);

        Assert.Equal(2, engine.Upserts.Count);
        Assert.Contains(engine.Upserts, upsert => upsert.Id == "cam1" && upsert.Urls.SequenceEqual([SourceUrl]));
        Assert.Contains(engine.Upserts, upsert => upsert.Id == "cam2" && upsert.Urls.SequenceEqual(["rtsp://192.168.1.11:554/sub"]));
    }

    [Fact]
    public async Task List_ReturnsMaskedUrlsSortedById()
    {
        var registry = new StreamRegistry(new FakeMediaEngine());
        await registry.AddAsync("cam2", "rtsp://192.168.1.11:554/sub", CancellationToken.None);
        await registry.AddAsync("cam1", SourceUrl, CancellationToken.None);

        var list = registry.List();

        Assert.Equal(["cam1", "cam2"], list.Select(static stream => stream.Id));
        Assert.Equal(MaskedUrl, list[0].Url);
    }
}
