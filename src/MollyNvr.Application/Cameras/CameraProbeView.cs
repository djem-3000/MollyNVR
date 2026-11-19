using MollyNvr.Application.Streams;
using MollyNvr.Cameras.Abstractions;
using DomainCameraProfile = MollyNvr.Domain.Cameras.CameraProfile;

namespace MollyNvr.Application.Cameras;

/// <summary>Camera profile representation for the API: RTSP URL without credentials.</summary>
public sealed record CameraProfileView(
    string Kind,
    string Id,
    string Name,
    string VideoCodec,
    int Width,
    int Height,
    double FrameRate,
    int BitrateKbps,
    bool AudioEnabled,
    string? AudioCodec,
    int? AudioSampleRate,
    int? AudioBitrateKbps,
    string RtspUrl,
    string? StreamId = null,
    string VideoPlayback = "passthrough",
    string? VideoTargetCodec = null,
    string AudioPlayback = "disabled",
    string? AudioTargetCodec = null)
{
    public static CameraProfileView From(CameraProfile profile)
    {
        var audioMode = profile.Audio.Enabled
            ? AudioPlaybackPolicy.Classify(profile.Audio.Codec)
            : AudioPlaybackMode.Disabled;

        return new(
            Kind: profile.Kind == CameraStreamProfileKind.Main ? "main" : "sub",
            Id: profile.Id,
            Name: profile.DisplayName,
            VideoCodec: profile.Video.Codec,
            Width: profile.Video.Width,
            Height: profile.Video.Height,
            FrameRate: profile.Video.FrameRate,
            BitrateKbps: profile.Video.BitrateKbps,
            AudioEnabled: profile.Audio.Enabled,
            AudioCodec: profile.Audio.Codec,
            AudioSampleRate: profile.Audio.SampleRate,
            AudioBitrateKbps: profile.Audio.Enabled ? profile.Audio.Bitrate : null,
            RtspUrl: StreamUrlMasker.Mask(profile.RtspUrl),
            VideoPlayback: VideoPlaybackPolicy.ToApiValue(profile.Video.Codec),
            VideoTargetCodec: VideoPlaybackPolicy.TargetCodecFor(profile.Video.Codec),
            AudioPlayback: AudioPlaybackPolicy.ToApiValue(audioMode),
            AudioTargetCodec: AudioPlaybackPolicy.TargetCodecFor(audioMode));
    }

    public static CameraProfileView FromDomain(DomainCameraProfile profile, string streamId, bool isMain)
    {
        var audioMode = profile.Audio is not null
            ? AudioPlaybackPolicy.Classify(profile.Audio.AudioCodec)
            : AudioPlaybackMode.Disabled;

        return new(
            Kind: isMain ? "main" : "sub",
            Id: profile.Id,
            Name: profile.Name,
            VideoCodec: profile.Video.Codec,
            Width: profile.Video.Width,
            Height: profile.Video.Height,
            FrameRate: profile.Video.FrameRate,
            BitrateKbps: profile.Video.BitrateKbps,
            AudioEnabled: profile.Audio is not null,
            AudioCodec: profile.Audio?.AudioCodec,
            AudioSampleRate: profile.Audio?.AudioSampleRate,
            AudioBitrateKbps: profile.Audio?.BitrateKbps,
            RtspUrl: StreamUrlMasker.Mask(profile.RtspUrl),
            StreamId: streamId,
            VideoPlayback: VideoPlaybackPolicy.ToApiValue(profile.Video.Codec),
            VideoTargetCodec: VideoPlaybackPolicy.TargetCodecFor(profile.Video.Codec),
            AudioPlayback: AudioPlaybackPolicy.ToApiValue(audioMode),
            AudioTargetCodec: AudioPlaybackPolicy.TargetCodecFor(audioMode));
    }
}

/// <summary>Camera communication protocol and its port.</summary>
/// <param name="Name">Protocol name; <c>null</c> when the port is not mapped to a default protocol.</param>
/// <param name="Role">Protocol role: <c>video</c> or <c>control</c>; <c>null</c> for an unmapped port.</param>
public sealed record CameraProtocolView(string? Name, int Port, string? Role);

/// <summary>Camera probe result for the API.</summary>
public sealed record CameraProbeView(
    string VendorKey,
    string Vendor,
    string Model,
    string Firmware,
    string? SerialNumber,
    string? DeviceName,
    IReadOnlyList<CameraProfileView> Profiles,
    IReadOnlyList<CameraProtocolView> Protocols)
{
    public static CameraProbeView From(
        string vendorKey,
        CameraProbeResult result,
        IReadOnlyList<int> openPorts) =>
        new(
            VendorKey: vendorKey,
            Vendor: result.Device.Vendor,
            Model: result.Device.Model,
            Firmware: result.Device.Firmware,
            SerialNumber: result.Device.SerialNumber,
            DeviceName: result.Device.DeviceName,
            Profiles: result.Profiles.Select(CameraProfileView.From).ToList(),
            Protocols: BuildProtocols(vendorKey, openPorts));

    private static IReadOnlyList<CameraProtocolView> BuildProtocols(
        string vendorKey,
        IReadOnlyList<int> openPorts) =>
        openPorts
            .Distinct()
            .Order()
            .Select(port =>
            {
                var matches = CameraPortCatalog.Match(vendorKey, port);

                return new CameraProtocolView(
                    Name: matches.Count == 0
                        ? null
                        : string.Join(", ", matches.Select(static match => match.Name)),
                    Port: port,
                    Role: matches.FirstOrDefault()?.Role);
            })
            .ToList();
}
