using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using MollyNvr.Application.Cameras;
using MollyNvr.Cameras.Abstractions;
using MollyNvr.Domain.Cameras;
using MollyNvr.Domain.Streams;
using AbstractionsCameraProfile = MollyNvr.Cameras.Abstractions.CameraProfile;
using DomainCameraProfile = MollyNvr.Domain.Cameras.CameraProfile;

namespace MollyNvr.Application.Tests;

public class CameraRegistryTests
{
    private static readonly CameraCredentials Credentials = new("admin", "secret");

    [Fact]
    public async Task AddAsync_ProbesCameraSavesItAndRegistersStreams()
    {
        var repository = new FakeRepository();
        var media = new FakeMediaEngine();
        var registry = CreateRegistry(repository, media);

        var camera = await registry.AddAsync(
            new CameraRegistration("Крыльцо", "192.168.1.10", null, "admin", "secret", null),
            CancellationToken.None);

        Assert.Equal("Крыльцо", camera.Name);
        Assert.Equal("192.168.1.10", camera.Host);
        Assert.Equal(2, camera.Profiles.Count);
        Assert.All(camera.Profiles, static profile => Assert.NotNull(profile.StreamId));
        Assert.Equal(camera.Id, repository.SavedCamera?.Id);
        Assert.Equal(camera.Name, repository.SavedCamera?.Name);
        Assert.Equal(Credentials, repository.SavedCredentials);
        Assert.Equal(2, media.Streams.Count);
        Assert.Contains($"camera-{camera.Id:N}-main", media.Streams.Keys);
        Assert.All(media.Streams.Values, static sources =>
            Assert.Contains(sources, static url => url.Contains("admin:secret@", StringComparison.Ordinal)));
        Assert.Contains(
            media.Streams[$"camera-{camera.Id:N}-main"],
            static url => url.StartsWith("ffmpeg:", StringComparison.Ordinal)
                && url.EndsWith("#audio=opus", StringComparison.Ordinal));
        Assert.DoesNotContain(
            media.Streams[$"camera-{camera.Id:N}-sub"],
            static url => url.StartsWith("ffmpeg:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AddAsync_UsesRequestedMainProfile()
    {
        var repository = new FakeRepository();
        var registry = CreateRegistry(repository, new FakeMediaEngine());

        var camera = await registry.AddAsync(
            new CameraRegistration(
                "Крыльцо",
                "192.168.1.10",
                null,
                "admin",
                "secret",
                null,
                SubProfileId: null,
                MainProfileId: "sub"),
            CancellationToken.None);

        var main = Assert.Single(camera.Profiles, profile => profile.Id == "sub");
        Assert.Equal("main", main.Kind);
        Assert.Equal("sub", repository.SavedCamera?.MainProfileId);
    }

    [Fact]
    public async Task AddAsync_UsesDeviceNameWhenNameNotProvided()
    {
        var registry = CreateRegistry(new FakeRepository(), new FakeMediaEngine());

        var camera = await registry.AddAsync(
            new CameraRegistration(null, "192.168.1.10", null, "admin", "secret", null),
            CancellationToken.None);

        Assert.Equal("Крыльцо", camera.Name);
    }

    [Fact]
    public async Task AddAsync_InvalidHost_Throws()
    {
        var registry = CreateRegistry(new FakeRepository(), new FakeMediaEngine());

        await Assert.ThrowsAsync<ArgumentException>(() => registry.AddAsync(
            new CameraRegistration("Камера", "not-an-ip", null, "admin", "secret", null),
            CancellationToken.None));
    }

    [Fact]
    public async Task AddAsync_MediaEngineFailure_StillSavesCamera()
    {
        var repository = new FakeRepository();
        var media = new FakeMediaEngine { FailUpserts = true };
        var registry = CreateRegistry(repository, media);

        var camera = await registry.AddAsync(
            new CameraRegistration("Крыльцо", "192.168.1.10", null, "admin", "secret", null),
            CancellationToken.None);

        Assert.NotNull(repository.SavedCamera);
        Assert.Equal(camera.Id, repository.SavedCamera.Id);
    }

    [Fact]
    public async Task AddAsync_EmptyPassword_Throws()
    {
        var registry = CreateRegistry(new FakeRepository(), new FakeMediaEngine());

        await Assert.ThrowsAsync<ArgumentException>(() => registry.AddAsync(
            new CameraRegistration("Камера", "192.168.1.10", null, "admin", null, null),
            CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_EmptyPassword_KeepsStoredCredentials()
    {
        var repository = new FakeRepository();
        var existing = CreateCamera("Старое имя");
        repository.Seed(existing, Credentials);
        var registry = CreateRegistry(repository, new FakeMediaEngine());

        var updated = await registry.UpdateAsync(
            existing.Id,
            new CameraRegistration("Новое имя", "192.168.1.10", null, "admin", "", null),
            CancellationToken.None);

        Assert.Equal("Новое имя", updated.Name);
        Assert.Null(repository.UpdatedCredentials);
        Assert.Equal(existing.Id, updated.Id);
    }

    [Fact]
    public async Task UpdateAsync_UnknownCamera_Throws()
    {
        var registry = CreateRegistry(new FakeRepository(), new FakeMediaEngine());

        await Assert.ThrowsAsync<CameraNotFoundException>(() => registry.UpdateAsync(
            Guid.NewGuid(),
            new CameraRegistration("Камера", "192.168.1.10", null, "admin", "secret", null),
            CancellationToken.None));
    }

    [Fact]
    public async Task RemoveAsync_DeletesCameraAndItsStreams()
    {
        var repository = new FakeRepository();
        var media = new FakeMediaEngine();
        var existing = CreateCamera("Камера");
        repository.Seed(existing, Credentials);
        var registry = CreateRegistry(repository, media);

        await registry.RemoveAsync(existing.Id, CancellationToken.None);

        Assert.Equal([existing.Id], repository.Removed);
        Assert.Equal(2, media.Removed.Count);
        Assert.Contains($"camera-{existing.Id:N}-main", media.Removed);
    }

    [Fact]
    public async Task SyncStreamsAsync_RegistersStreamsOfAllCameras()
    {
        var repository = new FakeRepository();
        var media = new FakeMediaEngine();
        var first = CreateCamera("Первая");
        var second = CreateCamera("Вторая");
        repository.Seed(first, Credentials);
        repository.Seed(second, Credentials);
        var registry = CreateRegistry(repository, media);

        await registry.SyncStreamsAsync(CancellationToken.None);

        Assert.Equal(4, media.Streams.Count);
    }

    [Fact]
    public async Task SetActiveAsync_Deactivate_RemovesStreams()
    {
        var repository = new FakeRepository();
        var media = new FakeMediaEngine();
        var camera = CreateCamera("Камера");
        repository.Seed(camera, Credentials);
        var registry = CreateRegistry(repository, media);

        var updated = await registry.SetActiveAsync(camera.Id, isActive: false, CancellationToken.None);

        Assert.False(updated.IsActive);
        Assert.Equal(2, media.Removed.Count);
    }

    [Fact]
    public async Task SetActiveAsync_Activate_RegistersStreams()
    {
        var repository = new FakeRepository();
        var media = new FakeMediaEngine();
        var camera = CreateCamera("Камера").SetActive(false, DateTimeOffset.UtcNow);
        repository.Seed(camera, Credentials);
        var registry = CreateRegistry(repository, media);

        var updated = await registry.SetActiveAsync(camera.Id, isActive: true, CancellationToken.None);

        Assert.True(updated.IsActive);
        Assert.Equal(2, media.Streams.Count);
    }

    [Fact]
    public async Task SetActiveAsync_UnknownCamera_Throws()
    {
        var registry = CreateRegistry(new FakeRepository(), new FakeMediaEngine());

        await Assert.ThrowsAsync<CameraNotFoundException>(
            () => registry.SetActiveAsync(Guid.NewGuid(), isActive: true, CancellationToken.None));
    }

    [Fact]
    public async Task SyncStreamsAsync_AddsH264TranscodeSourceForH265()
    {
        var repository = new FakeRepository();
        var media = new FakeMediaEngine();
        var camera = CreateCamera("Камера", videoCodec: "H.265");
        repository.Seed(camera, Credentials);
        var registry = CreateRegistry(repository, media);

        await registry.SyncStreamsAsync(CancellationToken.None);

        Assert.Contains(
            media.Streams[$"camera-{camera.Id:N}-main"],
            static url => url.Contains("#video=h264", StringComparison.Ordinal)
                && url.Contains("#hardware", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SyncStreamsAsync_DoesNotTranscodeH264()
    {
        var repository = new FakeRepository();
        var media = new FakeMediaEngine();
        var camera = CreateCamera("Камера", videoCodec: "H.264");
        repository.Seed(camera, Credentials);
        var registry = CreateRegistry(repository, media);

        await registry.SyncStreamsAsync(CancellationToken.None);

        Assert.DoesNotContain(
            media.Streams[$"camera-{camera.Id:N}-main"],
            static url => url.Contains("#video=h264", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SyncStreamsAsync_DoesNotTranscodeG7221Audio()
    {
        var repository = new FakeRepository();
        var media = new FakeMediaEngine();
        var camera = CreateCamera("Камера", audioCodec: "G7221");
        repository.Seed(camera, Credentials);
        var registry = CreateRegistry(repository, media);

        await registry.SyncStreamsAsync(CancellationToken.None);

        Assert.DoesNotContain(
            media.Streams[$"camera-{camera.Id:N}-main"],
            static url => url.StartsWith("ffmpeg:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FindAsync_ReturnsNullForUnknownCamera()
    {
        var registry = CreateRegistry(new FakeRepository(), new FakeMediaEngine());

        Assert.Null(await registry.FindAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task SyncStreamsAsync_RegistersOnlySelectedSubstream()
    {
        var repository = new FakeRepository();
        var media = new FakeMediaEngine();
        var camera = Camera.Register(
            "Камера",
            "192.168.1.10",
            554,
            "onvif",
            "ONVIF",
            "CX820",
            "1.0",
            "SN",
            CreateProfilesWithSubstreams(),
            mainProfileId: "main",
            DateTimeOffset.UtcNow,
            subProfileId: "sub2");
        repository.Seed(camera, Credentials);
        var registry = CreateRegistry(repository, media);

        await registry.SyncStreamsAsync(CancellationToken.None);

        Assert.Equal(2, media.Streams.Count);
        Assert.Contains(
            media.Streams[$"camera-{camera.Id:N}-sub"],
            static url => url.Contains("/sub2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NegotiateWhepAsync_ExplicitSubstreamUsesRequestedProfile()
    {
        var repository = new FakeRepository();
        var media = new FakeMediaEngine();
        var camera = Camera.Register(
            "Камера",
            "192.168.1.10",
            554,
            "onvif",
            "ONVIF",
            "CX820",
            "1.0",
            "SN",
            CreateProfilesWithSubstreams(),
            mainProfileId: "main",
            DateTimeOffset.UtcNow,
            subProfileId: "sub");
        repository.Seed(camera, Credentials);
        var registry = CreateRegistry(repository, media);

        await registry.NegotiateWhepAsync(
            camera.Id,
            CameraStreamProfileKind.Sub,
            "v=0 offer",
            CancellationToken.None,
            profileId: "sub2");

        Assert.Contains(
            media.Streams[$"camera-{camera.Id:N}-sub"],
            static url => url.Contains("/sub2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UpdateSettingsAsync_ChangesNameAndSelectedSubstreamWithoutProbe()
    {
        var repository = new FakeRepository();
        var media = new FakeMediaEngine();
        var camera = Camera.Register(
            "Камера",
            "192.168.1.10",
            554,
            "onvif",
            "ONVIF",
            "CX820",
            "1.0",
            "SN",
            CreateProfilesWithSubstreams(),
            mainProfileId: "main",
            DateTimeOffset.UtcNow,
            subProfileId: "sub");
        repository.Seed(camera, Credentials);
        var registry = CreateRegistry(repository, media);

        var updated = await registry.UpdateSettingsAsync(
            camera.Id,
            "Крыльцо",
            "sub2",
            CancellationToken.None);

        var saved = await repository.FindAsync(camera.Id, CancellationToken.None);

        Assert.Equal("Крыльцо", updated.Name);
        Assert.Equal("sub2", updated.SubProfileId);
        Assert.Equal("Крыльцо", saved?.Name);
        Assert.Equal("sub2", saved?.SubProfileId);
        Assert.Contains(
            media.Streams[$"camera-{camera.Id:N}-sub"],
            static url => url.Contains("/sub2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UpdateSettingsAsync_UnknownSubstream_Throws()
    {
        var repository = new FakeRepository();
        var camera = CreateCamera("Камера");
        repository.Seed(camera, Credentials);
        var registry = CreateRegistry(repository, new FakeMediaEngine());

        await Assert.ThrowsAsync<ArgumentException>(() => registry.UpdateSettingsAsync(
            camera.Id,
            "Камера",
            "missing",
            CancellationToken.None));
    }

    [Fact]
    public async Task NegotiateWhepAsync_RegistersStreamAndReturnsAnswer()
    {
        var repository = new FakeRepository();
        var media = new FakeMediaEngine { Answer = "v=0 answer" };
        var camera = CreateCamera("Камера");
        repository.Seed(camera, Credentials);
        var registry = CreateRegistry(repository, media);

        var answer = await registry.NegotiateWhepAsync(
            camera.Id,
            CameraStreamProfileKind.Main,
            "v=0 offer",
            CancellationToken.None);

        Assert.Equal("v=0 answer", answer);
        var streamId = $"camera-{camera.Id:N}-main";
        Assert.Contains(streamId, media.Streams.Keys);
        var sources = media.Streams[streamId];
        Assert.Contains(sources, static url => url.Contains("admin:secret@", StringComparison.Ordinal));
        Assert.Contains(sources, static url => url.StartsWith("ffmpeg:", StringComparison.Ordinal));
        Assert.Contains($"{streamId}:v=0 offer", media.Negotiated);
    }

    [Fact]
    public async Task NegotiateWhepAsync_MissingProfile_Throws()
    {
        var repository = new FakeRepository();
        var camera = Camera.Register(
            "Камера",
            "192.168.1.10",
            554,
            "onvif",
            "ONVIF",
            "CX820",
            "1.0",
            null,
            [CreateProfiles()[0]],
            mainProfileId: "main",
            DateTimeOffset.UtcNow);
        repository.Seed(camera, Credentials);
        var registry = CreateRegistry(repository, new FakeMediaEngine());

        await Assert.ThrowsAsync<CameraOperationNotSupportedException>(() => registry.NegotiateWhepAsync(
            camera.Id,
            CameraStreamProfileKind.Sub,
            "v=0 offer",
            CancellationToken.None));
    }

    [Fact]
    public async Task NegotiateWhepAsync_UnknownCamera_Throws()
    {
        var registry = CreateRegistry(new FakeRepository(), new FakeMediaEngine());

        await Assert.ThrowsAsync<CameraNotFoundException>(() => registry.NegotiateWhepAsync(
            Guid.NewGuid(),
            CameraStreamProfileKind.Sub,
            "v=0 offer",
            CancellationToken.None));
    }

    [Fact]
    public async Task NegotiateWhepAsync_EmptyOffer_Throws()
    {
        var registry = CreateRegistry(new FakeRepository(), new FakeMediaEngine());

        await Assert.ThrowsAsync<ArgumentException>(() => registry.NegotiateWhepAsync(
            Guid.NewGuid(),
            CameraStreamProfileKind.Sub,
            "   ",
            CancellationToken.None));
    }

    private static CameraRegistry CreateRegistry(FakeRepository repository, FakeMediaEngine media) =>
        new(
            repository,
            new CameraProbeService([new FakeModule()], new NullMediaProbe(), new NullFrameGrabber(), new NullPortScanner()),
            media,
            NullLogger<CameraRegistry>.Instance);

    private static Camera CreateCamera(string name, string audioCodec = "AAC", string videoCodec = "H.264") =>
        Camera.Register(
            name,
            "192.168.1.10",
            554,
            "onvif",
            "ONVIF",
            "CX820",
            "1.0",
            "SN",
            CreateProfiles(audioCodec, videoCodec),
            mainProfileId: "main",
            DateTimeOffset.UtcNow);

    private static IReadOnlyList<DomainCameraProfile> CreateProfiles(
        string audioCodec = "AAC",
        string videoCodec = "H.264") =>
    [
        new(
            "main",
            "Main",
            new CameraProfileVideo(videoCodec, 1920, 1080, 25, 4096),
            new CameraProfileAudio(audioCodec, 16000, 64),
            "rtsp://192.168.1.10:554/main"),
        new(
            "sub",
            "Sub",
            new CameraProfileVideo("H.264", 640, 360, 10, 512),
            null,
            "rtsp://192.168.1.10:554/sub"),
    ];

    private static IReadOnlyList<DomainCameraProfile> CreateProfilesWithSubstreams() =>
    [
        .. CreateProfiles(),
        new(
            "sub2",
            "Sub 2",
            new CameraProfileVideo("H.264", 320, 180, 5, 128),
            null,
            "rtsp://192.168.1.10:554/sub2"),
    ];

    private sealed class FakeRepository : ICameraRepository
    {
        private readonly Dictionary<Guid, Camera> _cameras = [];
        private readonly Dictionary<Guid, CameraCredentials> _credentials = [];

        public Camera? SavedCamera { get; private set; }

        public CameraCredentials? SavedCredentials { get; private set; }

        public CameraCredentials? UpdatedCredentials { get; private set; }

        public List<Guid> Removed { get; } = [];

        public void Seed(Camera camera, CameraCredentials credentials)
        {
            _cameras[camera.Id] = camera;
            _credentials[camera.Id] = credentials;
        }

        public Task<IReadOnlyList<Camera>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Camera>>(_cameras.Values.OrderBy(static camera => camera.Name).ToList());

        public Task<Camera?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_cameras.GetValueOrDefault(id));

        public Task AddAsync(Camera camera, CameraCredentials credentials, CancellationToken cancellationToken)
        {
            SavedCamera = camera;
            SavedCredentials = credentials;
            _cameras[camera.Id] = camera;
            _credentials[camera.Id] = credentials;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Camera camera, CameraCredentials? credentials, CancellationToken cancellationToken)
        {
            UpdatedCredentials = credentials;
            _cameras[camera.Id] = camera;

            if (credentials is not null)
            {
                _credentials[camera.Id] = credentials;
            }

            return Task.CompletedTask;
        }

        public Task RemoveAsync(Guid id, CancellationToken cancellationToken)
        {
            Removed.Add(id);
            _cameras.Remove(id);
            _credentials.Remove(id);
            return Task.CompletedTask;
        }

        public Task<CameraCredentials?> GetCredentialsAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_credentials.GetValueOrDefault(id));

        public Task<Camera?> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
        {
            if (!_cameras.TryGetValue(id, out var camera))
            {
                return Task.FromResult<Camera?>(null);
            }

            var updated = camera.SetActive(isActive, DateTimeOffset.UtcNow);
            _cameras[id] = updated;
            return Task.FromResult<Camera?>(updated);
        }
    }

    private sealed class FakeMediaEngine : IMediaEngine
    {
        public bool FailUpserts { get; init; }

        public string Answer { get; init; } = "v=0";

        public List<string> Negotiated { get; } = [];

        public Dictionary<string, IReadOnlyList<string>> Streams { get; } = [];

        public List<string> Removed { get; } = [];

        public Task<IReadOnlyList<string>> GetStreamIdsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(Streams.Keys.ToList());

        public Task UpsertStreamAsync(
            StreamId id,
            IReadOnlyList<string> sourceUrls,
            CancellationToken cancellationToken)
        {
            if (FailUpserts)
            {
                throw new HttpRequestException("go2rtc недоступен");
            }

            Streams[id.Value] = sourceUrls;
            return Task.CompletedTask;
        }

        public Task RemoveStreamAsync(StreamId id, CancellationToken cancellationToken)
        {
            Removed.Add(id.Value);
            Streams.Remove(id.Value);
            return Task.CompletedTask;
        }

        public Task<string> NegotiateWebRtcAsync(StreamId id, string sdpOffer, CancellationToken cancellationToken)
        {
            Negotiated.Add($"{id.Value}:{sdpOffer}");
            return Task.FromResult(Answer);
        }
    }
    private sealed class FakeModule : ICameraModule
    {
        public string Key => "onvif";

        public string DisplayName => "ONVIF";

        public Task<bool> CanHandleAsync(CameraEndpoint endpoint, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<CameraProbeResult> ProbeAsync(
            CameraEndpoint endpoint,
            CameraCredentials credentials,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CameraProbeResult(
                new CameraDeviceInfo("ONVIF", "CX820", "1.0", "SN", "Крыльцо"),
                [
                    new AbstractionsCameraProfile(
                        CameraStreamProfileKind.Main,
                        "main",
                        "Main",
                        new CameraVideoSettings("H.264", 1920, 1080, 25, 4096),
                        new CameraAudioSettings(true, "AAC", 16000, 64),
                        "rtsp://192.168.1.10:554/main"),
                    new AbstractionsCameraProfile(
                        CameraStreamProfileKind.Sub,
                        "sub",
                        "Sub",
                        new CameraVideoSettings("H.264", 640, 360, 10, 512),
                        new CameraAudioSettings(false, null, null, null),
                        "rtsp://192.168.1.10:554/sub"),
                ]));

        public Task<byte[]> GetSnapshotAsync(
            CameraEndpoint endpoint,
            CameraCredentials credentials,
            AbstractionsCameraProfile profile,
            CancellationToken cancellationToken) =>
            Task.FromResult<byte[]>([]);
    }

    private sealed class NullPortScanner : IPortScanner
    {
        public Task<IReadOnlyList<int>> FindOpenPortsAsync(
            IPAddress host,
            IReadOnlyList<int> ports,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<int>>([]);
    }

    private sealed class NullMediaProbe : IRtspMediaProbe
    {
        public Task<RtspMediaInfo?> ProbeAsync(
            Uri rtspUri,
            CameraCredentials credentials,
            CancellationToken cancellationToken) =>
            Task.FromResult<RtspMediaInfo?>(null);
    }

    private sealed class NullFrameGrabber : IFrameGrabber
    {
        public Task<byte[]> GrabJpegAsync(string sourceUrl, CancellationToken cancellationToken) =>
            Task.FromResult<byte[]>([]);
    }
}
