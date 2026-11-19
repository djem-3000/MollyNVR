using MollyNvr.Cameras.Abstractions;
using MollyNvr.Domain.Cameras;

namespace MollyNvr.Application.Cameras;

/// <summary>Camera storage. Credentials are stored separately and encrypted.</summary>
public interface ICameraRepository
{
        /// <summary>All cameras sorted by name.</summary>
    Task<IReadOnlyList<Camera>> ListAsync(CancellationToken cancellationToken);

        /// <summary>Camera by identifier, or <c>null</c>.</summary>
    Task<Camera?> FindAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>Saves a new camera together with its credentials.</summary>
    Task AddAsync(Camera camera, CameraCredentials credentials, CancellationToken cancellationToken);

    /// <summary>
        /// Updates the camera. <paramref name="credentials"/> = <c>null</c> keeps the current credentials.
    /// </summary>
    Task UpdateAsync(Camera camera, CameraCredentials? credentials, CancellationToken cancellationToken);

        /// <summary>Removes the camera and its credentials.</summary>
    Task RemoveAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>Decrypted camera credentials, or <c>null</c>.</summary>
    Task<CameraCredentials?> GetCredentialsAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>Toggles the active flag; returns the updated camera, or <c>null</c> when it does not exist.</summary>
    Task<Camera?> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken);
}
