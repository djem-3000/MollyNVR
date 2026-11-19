namespace MollyNvr.Application.Cameras;

/// <summary>Parameters for adding or updating a camera (the password is never returned).</summary>
public sealed record CameraRegistration(
    string? Name,
    string? Host,
    int? RtspPort,
    string? UserName,
    string? Password,
    string? VendorKey,
    string? SubProfileId = null,
    string? MainProfileId = null);
