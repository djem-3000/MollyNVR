using System.Security.Cryptography;
using System.Text.Json;
using MollyNvr.Application.Cameras;
using MollyNvr.Cameras.Abstractions;
using MollyNvr.Infrastructure.Storage;

namespace MollyNvr.Infrastructure.Security;

/// <summary>
/// AES-GCM encryption of camera credentials. The key (32 bytes) is created on first start
/// and stored in <c>/data/secret.key</c>; only ciphertext reaches the database.
/// </summary>
public sealed class AesGcmCameraSecretStore : ICameraSecretStore
{
    private const byte FormatVersion = 1;
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public AesGcmCameraSecretStore(StorageOptions options)
    {
        _key = LoadOrCreateKey(options.SecretKeyPath);
    }

    public string Protect(CameraCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(
            new SecretPayload(credentials.UserName, credentials.Password));

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var payload = new byte[1 + NonceSize + TagSize + ciphertext.Length];
        payload[0] = FormatVersion;
        nonce.CopyTo(payload, 1);
        tag.CopyTo(payload, 1 + NonceSize);
        ciphertext.CopyTo(payload, 1 + NonceSize + TagSize);

        return Convert.ToBase64String(payload);
    }

    public CameraCredentials Unprotect(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
                        throw new InvalidOperationException("The encrypted camera credentials are empty.");
        }

        byte[] data;
        try
        {
            data = Convert.FromBase64String(payload);
        }
        catch (FormatException exception)
        {
                        throw new InvalidOperationException("The camera credentials are corrupted.", exception);
        }

        if (data.Length <= 1 + NonceSize + TagSize || data[0] != FormatVersion)
        {
                        throw new InvalidOperationException("The camera credentials are corrupted.");
        }

        var nonce = data.AsSpan(1, NonceSize);
        var tag = data.AsSpan(1 + NonceSize, TagSize);
        var ciphertext = data.AsSpan(1 + NonceSize + TagSize);
        var plaintext = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException(
                                "Failed to decrypt the camera credentials: the key does not match or the data is corrupted.",
                exception);
        }

        try
        {
            var secret = JsonSerializer.Deserialize<SecretPayload>(plaintext)
                                ?? throw new InvalidOperationException("The camera credentials are corrupted.");

            return new CameraCredentials(secret.UserName, secret.Password);
        }
        catch (JsonException exception)
        {
                        throw new InvalidOperationException("The camera credentials are corrupted.", exception);
        }
    }

    private static byte[] LoadOrCreateKey(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(path))
        {
            var existing = File.ReadAllBytes(path);
            if (existing.Length != KeySize)
            {
                throw new InvalidOperationException(
                                        $"The key file '{path}' is corrupted: {KeySize} bytes expected, {existing.Length} received.");
            }

            return existing;
        }

        var key = RandomNumberGenerator.GetBytes(KeySize);
        File.WriteAllBytes(path, key);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return key;
    }

    private sealed record SecretPayload(string UserName, string Password);
}
