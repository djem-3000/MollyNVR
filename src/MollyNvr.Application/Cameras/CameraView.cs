using MollyNvr.Domain.Cameras;

namespace MollyNvr.Application.Cameras;

/// <summary>Saved camera for the API (RTSP URL without credentials).</summary>
public sealed record CameraView(
    Guid Id,
    string Name,
    string Host,
    int RtspPort,
    string VendorKey,
    string Vendor,
    string Model,
    string Firmware,
    string? SerialNumber,
    bool IsActive,
    IReadOnlyList<CameraProfileView> Profiles,
    string SubProfileId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static CameraView From(Camera camera)
    {
        var profiles = camera.Profiles
            .OrderByDescending(profile => string.Equals(profile.Id, camera.MainProfileId, StringComparison.Ordinal))
            .Select(profile =>
            {
                var isMain = string.Equals(profile.Id, camera.MainProfileId, StringComparison.Ordinal);
                var streamId = CameraStreamNames.ForProfile(camera.Id, isMain).Value;
                return CameraProfileView.FromDomain(profile, streamId, isMain);
            })
            .ToList();

        return new(
            Id: camera.Id,
            Name: camera.Name,
            Host: camera.Host,
            RtspPort: camera.RtspPort,
            VendorKey: camera.VendorKey,
            Vendor: camera.Vendor,
            Model: camera.Model,
            Firmware: camera.Firmware,
            SerialNumber: camera.SerialNumber,
            IsActive: camera.IsActive,
            Profiles: profiles,
            SubProfileId: camera.SubProfileId,
            CreatedAt: camera.CreatedAt,
            UpdatedAt: camera.UpdatedAt);
    }
}

/// <summary>Identifiers of the camera streams in the media engine.</summary>
public static class CameraStreamNames
{
        /// <summary>Camera profile stream: <c>camera-{id}-main</c> or <c>camera-{id}-sub</c>.</summary>
    public static Domain.Streams.StreamId ForProfile(Guid cameraId, bool isMain) =>
        Domain.Streams.StreamId.Create($"camera-{cameraId:N}-{(isMain ? "main" : "sub")}");
}
