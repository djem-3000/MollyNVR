namespace MollyNvr.Cameras.Abstractions;

/// <summary>Additional network parameters a module can request from the camera.</summary>
public interface ICameraNetworkInfoProvider
{
    /// <summary>
        /// Returns the DHCP state, or <c>null</c> when the camera does not report the parameter.
    /// </summary>
    Task<bool?> GetDhcpEnabledAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CancellationToken cancellationToken);
}
