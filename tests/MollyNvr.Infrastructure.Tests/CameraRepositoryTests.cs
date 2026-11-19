using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MollyNvr.Application.Cameras;
using MollyNvr.Domain.Cameras;
using MollyNvr.Infrastructure.Persistence;
using MollyNvr.Infrastructure.Security;
using CameraCredentials = MollyNvr.Cameras.Abstractions.CameraCredentials;

namespace MollyNvr.Infrastructure.Tests;

public class CameraRepositoryTests
{
    [Fact]
    public async Task AddAsync_PersistsCameraProfilesAndEncryptedCredentials()
    {
        using var directory = new TempDirectory();
        var (provider, repository) = await CreateRepositoryAsync(directory.Path);

        try
        {
                        var camera = CreateCamera("Porch");
            var credentials = new CameraCredentials("admin", "super-secret");
            await repository.AddAsync(camera, credentials, CancellationToken.None);

            var saved = await repository.FindAsync(camera.Id, CancellationToken.None);

            Assert.NotNull(saved);
                        Assert.Equal("Porch", saved.Name);
            Assert.Equal(2, saved.Profiles.Count);
            Assert.Equal("main", saved.MainProfileId);
            Assert.Equal("sub", saved.SubProfileId);
            Assert.Equal("AAC", saved.Profiles[0].Audio?.AudioCodec);
            Assert.Equal(16000, saved.Profiles[0].Audio?.AudioSampleRate);
            Assert.Equal(64, saved.Profiles[0].Audio?.BitrateKbps);
            Assert.Null(saved.Profiles[1].Audio);
            Assert.Equal(credentials, await repository.GetCredentialsAsync(camera.Id, CancellationToken.None));

            var factory = provider.GetRequiredService<IDbContextFactory<MollyNvrDbContext>>();
            await using var db = await factory.CreateDbContextAsync();
            var stored = await db.Cameras.AsNoTracking().Select(static item => item.Credentials).SingleAsync();
            Assert.False(stored.Contains("super-secret", StringComparison.Ordinal));
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task UpdateAsync_WithoutCredentials_KeepsPasswordAndReplacesProfiles()
    {
        using var directory = new TempDirectory();
        var (provider, repository) = await CreateRepositoryAsync(directory.Path);

        try
        {
                        var camera = CreateCamera("Old name");
            var credentials = new CameraCredentials("admin", "secret");
            await repository.AddAsync(camera, credentials, CancellationToken.None);

            var updated = camera.Update(
                                "New name",
                camera.Host,
                camera.RtspPort,
                camera.VendorKey,
                camera.Vendor,
                camera.Model,
                camera.Firmware,
                camera.SerialNumber,
                [CreateProfiles()[0]],
                mainProfileId: "main",
                DateTimeOffset.UtcNow);

            await repository.UpdateAsync(updated, credentials: null, CancellationToken.None);

            var saved = await repository.FindAsync(camera.Id, CancellationToken.None);

            Assert.NotNull(saved);
                        Assert.Equal("New name", saved.Name);
            Assert.Single(saved.Profiles);
            Assert.Equal(credentials, await repository.GetCredentialsAsync(camera.Id, CancellationToken.None));
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task UpdateAsync_PersistsSelectedSubstream()
    {
        using var directory = new TempDirectory();
        var (provider, repository) = await CreateRepositoryAsync(directory.Path);

        try
        {
                        var camera = CreateCamera("Camera");
            await repository.AddAsync(camera, new CameraCredentials("admin", "secret"), CancellationToken.None);

                        var updated = camera.UpdateSettings("Porch", "sub", DateTimeOffset.UtcNow);
            await repository.UpdateAsync(updated, credentials: null, CancellationToken.None);

            var saved = await repository.FindAsync(camera.Id, CancellationToken.None);

            Assert.NotNull(saved);
                        Assert.Equal("Porch", saved.Name);
            Assert.Equal("sub", saved.SubProfileId);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task RemoveAsync_DeletesCameraAndProfiles()
    {
        using var directory = new TempDirectory();
        var (provider, repository) = await CreateRepositoryAsync(directory.Path);

        try
        {
                        var camera = CreateCamera("Camera");
            await repository.AddAsync(camera, new CameraCredentials("admin", "secret"), CancellationToken.None);

            await repository.RemoveAsync(camera.Id, CancellationToken.None);

            Assert.Empty(await repository.ListAsync(CancellationToken.None));

            var factory = provider.GetRequiredService<IDbContextFactory<MollyNvrDbContext>>();
            await using var db = await factory.CreateDbContextAsync();
            Assert.Equal(0, await db.CameraProfiles.CountAsync());
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task SetActiveAsync_TogglesFlagAndKeepsCredentials()
    {
        using var directory = new TempDirectory();
        var (provider, repository) = await CreateRepositoryAsync(directory.Path);

        try
        {
                        var camera = CreateCamera("Camera");
            var credentials = new CameraCredentials("admin", "secret");
            await repository.AddAsync(camera, credentials, CancellationToken.None);

            var deactivated = await repository.SetActiveAsync(camera.Id, isActive: false, CancellationToken.None);
            var saved = await repository.FindAsync(camera.Id, CancellationToken.None);

            Assert.NotNull(deactivated);
            Assert.False(deactivated.IsActive);
            Assert.NotNull(saved);
            Assert.False(saved.IsActive);
            Assert.Equal(credentials, await repository.GetCredentialsAsync(camera.Id, CancellationToken.None));
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task ListAsync_OrdersCamerasByName()
    {
        using var directory = new TempDirectory();
        var (provider, repository) = await CreateRepositoryAsync(directory.Path);

        try
        {
                        await repository.AddAsync(CreateCamera("Apple"), new CameraCredentials("admin", "secret"), CancellationToken.None);
                        await repository.AddAsync(CreateCamera("Apricot"), new CameraCredentials("admin", "secret"), CancellationToken.None);

            var cameras = await repository.ListAsync(CancellationToken.None);

                        Assert.Equal(["Apple", "Apricot"], cameras.Select(static camera => camera.Name));
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetCredentialsAsync_UnknownCamera_ReturnsNull()
    {
        using var directory = new TempDirectory();
        var (provider, repository) = await CreateRepositoryAsync(directory.Path);

        try
        {
            Assert.Null(await repository.GetCredentialsAsync(Guid.NewGuid(), CancellationToken.None));
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    private static async Task<(ServiceProvider Provider, ICameraRepository Repository)> CreateRepositoryAsync(
        string directory)
    {
        var storage = TestSupport.CreateStorageOptions(directory);
        var services = new ServiceCollection();
        services.AddSingleton(storage);
        services.AddSingleton<ICameraSecretStore, AesGcmCameraSecretStore>();
        services.AddDbContextFactory<MollyNvrDbContext>(options =>
            options.UseSqlite($"Data Source={storage.DatabasePath}"));
        services.AddSingleton<ICameraRepository, CameraRepository>();

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<MollyNvrDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();

        return (provider, provider.GetRequiredService<ICameraRepository>());
    }

    private static Camera CreateCamera(string name) =>
        Camera.Register(
            name,
            "192.168.1.10",
            554,
            "onvif",
            "ONVIF",
            "CX820",
            "1.0",
            "SN",
            CreateProfiles(),
            mainProfileId: "main",
            DateTimeOffset.UtcNow);

    private static IReadOnlyList<CameraProfile> CreateProfiles() =>
    [
        new(
            "main",
            "Main",
            new CameraProfileVideo("H.264", 1920, 1080, 25, 4096),
            new CameraProfileAudio("AAC", 16000, 64),
            "rtsp://192.168.1.10:554/main"),
        new(
            "sub",
            "Sub",
            new CameraProfileVideo("H.264", 640, 360, 10, 512),
            null,
            "rtsp://192.168.1.10:554/sub"),
    ];
}
