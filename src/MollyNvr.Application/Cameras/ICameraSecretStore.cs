using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Application.Cameras;

/// <summary>Encryption of camera credentials before they are stored in the database.</summary>
public interface ICameraSecretStore
{
        /// <summary>Encrypts the credentials into a string for storage.</summary>
    string Protect(CameraCredentials credentials);

        /// <summary>Decrypts the credentials; throws when the data is corrupted.</summary>
    CameraCredentials Unprotect(string payload);
}
