namespace MollyNvr.Cameras.Abstractions;

/// <summary>Camera vendor module: probe, stream profiles, snapshot.</summary>
public interface ICameraModule
{
        /// <summary>Module key (for example, <c>onvif</c>).</summary>
    string Key { get; }

        /// <summary>Vendor display name.</summary>
    string DisplayName { get; }

    /// <summary>
        /// Quick check whether the device responds at this address over the module protocol
        /// (without credentials). Used for discovery and module enumeration.
    /// </summary>
    Task<bool> CanHandleAsync(CameraEndpoint endpoint, CancellationToken cancellationToken);

        /// <summary>Device probe: details, profiles and streams.</summary>
        /// <exception cref="CameraAuthenticationException">The camera rejected the credentials.</exception>
        /// <exception cref="CameraUnavailableException">The device is unavailable or does not respond over the protocol.</exception>
    Task<CameraProbeResult> ProbeAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CancellationToken cancellationToken);

        /// <summary>JPEG snapshot of a camera profile.</summary>
    Task<byte[]> GetSnapshotAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CameraProfile profile,
        CancellationToken cancellationToken);
}
