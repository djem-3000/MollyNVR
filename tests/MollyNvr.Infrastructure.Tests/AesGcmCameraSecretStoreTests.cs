using MollyNvr.Cameras.Abstractions;
using MollyNvr.Infrastructure.Security;
using MollyNvr.Infrastructure.Storage;

namespace MollyNvr.Infrastructure.Tests;

public class AesGcmCameraSecretStoreTests
{
    [Fact]
    public void ProtectAndUnprotect_RoundTripsCredentials()
    {
        using var directory = new TempDirectory();
        var store = CreateStore(directory.Path);
        var credentials = new CameraCredentials("admin", "p@ss:word/тест");

        var payload = store.Protect(credentials);

        Assert.DoesNotContain("p@ss", payload, StringComparison.Ordinal);
        Assert.Equal(credentials, store.Unprotect(payload));
    }

    [Fact]
    public void Protect_SameCredentials_ProducesDifferentPayloads()
    {
        using var directory = new TempDirectory();
        var store = CreateStore(directory.Path);
        var credentials = new CameraCredentials("admin", "secret");

        Assert.NotEqual(store.Protect(credentials), store.Protect(credentials));
    }

    [Fact]
    public void Unprotect_TamperedPayload_Throws()
    {
        using var directory = new TempDirectory();
        var store = CreateStore(directory.Path);
        var payload = store.Protect(new CameraCredentials("admin", "secret"));
        var bytes = Convert.FromBase64String(payload);
        bytes[^1] ^= 0xFF;

        Assert.Throws<InvalidOperationException>(() => store.Unprotect(Convert.ToBase64String(bytes)));
    }

    [Fact]
    public void Unprotect_OtherKey_Throws()
    {
        using var first = new TempDirectory();
        using var second = new TempDirectory();
        var payload = CreateStore(first.Path).Protect(new CameraCredentials("admin", "secret"));

        Assert.Throws<InvalidOperationException>(() => CreateStore(second.Path).Unprotect(payload));
    }

    [Fact]
    public void Constructor_CreatesKeyFile()
    {
        using var directory = new TempDirectory();

        _ = CreateStore(directory.Path);

        Assert.Equal(32, File.ReadAllBytes(Path.Combine(directory.Path, "secret.key")).Length);
    }

    private static AesGcmCameraSecretStore CreateStore(string directory) =>
        new(TestSupport.CreateStorageOptions(directory));
}
