namespace MollyNvr.Domain.Cameras;

/// <summary>Snapshot of a stream profile saved when the camera was added (without credentials).</summary>
public sealed record CameraProfile(
    string Id,
    string Name,
    CameraProfileVideo Video,
    CameraProfileAudio? Audio,
    string RtspUrl);

/// <summary>Video codec parameters of a profile.</summary>
public sealed record CameraProfileVideo(
    string Codec,
    int Width,
    int Height,
    double FrameRate,
    int BitrateKbps);

/// <summary>Audio codec parameters of a profile.</summary>
public sealed record CameraProfileAudio(
    string AudioCodec,
    int AudioSampleRate,
    int BitrateKbps);
