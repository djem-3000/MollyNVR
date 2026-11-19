namespace MollyNvr.Cameras.Abstractions;

/// <summary>Device details obtained by the vendor module.</summary>
public sealed record CameraDeviceInfo(
    string Vendor,
    string Model,
    string Firmware,
    string? SerialNumber,
    string? DeviceName);

/// <summary>Purpose of the stream profile.</summary>
public enum CameraStreamProfileKind
{
        /// <summary>Main stream — maximum resolution.</summary>
    Main = 0,

        /// <summary>Substream — low resolution for the tile grid.</summary>
    Sub = 1,
}

/// <summary>Video codec parameters of a profile.</summary>
public sealed record CameraVideoSettings(
    string Codec,
    int Width,
    int Height,
    double FrameRate,
    int BitrateKbps);

/// <summary>Audio codec parameters of a profile.</summary>
public sealed record CameraAudioSettings(bool Enabled, string? Codec, int? SampleRate, int? Bitrate);

/// <summary>Camera stream profile (main stream or substream).</summary>
public sealed record CameraProfile(
    CameraStreamProfileKind Kind,
    string Id,
    string? Name,
    CameraVideoSettings Video,
    CameraAudioSettings Audio,
    string RtspUrl)
{
        public string DisplayName => Name ?? (Kind == CameraStreamProfileKind.Main ? "Main stream" : "Substream");
}

/// <summary>Camera probe result produced by a vendor module.</summary>
public sealed record CameraProbeResult(
    CameraDeviceInfo Device,
    IReadOnlyList<CameraProfile> Profiles)
{
    public CameraProfile? MainProfile =>
        Profiles.FirstOrDefault(static profile => profile.Kind == CameraStreamProfileKind.Main);

    public CameraProfile? SubProfile =>
        Profiles.FirstOrDefault(static profile => profile.Kind == CameraStreamProfileKind.Sub);
}
