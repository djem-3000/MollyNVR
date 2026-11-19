using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Application.Cameras;

/// <summary>
/// Camera probe target: network address, credentials and (optionally) an explicitly selected vendor module.
/// </summary>
public sealed record CameraTarget(CameraEndpoint Endpoint, CameraCredentials Credentials, string? VendorKey = null);
