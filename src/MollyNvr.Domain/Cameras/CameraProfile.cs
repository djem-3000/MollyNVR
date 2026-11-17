namespace MollyNvr.Domain.Cameras;

/// <summary>Слепок профиля потока, сохранённый при добавлении камеры (без учётных данных).</summary>
public sealed record CameraProfile(
    string Id,
    string Name,
    CameraProfileVideo Video,
    CameraProfileAudio? Audio,
    string RtspUrl);

/// <summary>Параметры видеокодека профиля.</summary>
public sealed record CameraProfileVideo(
    string Codec,
    int Width,
    int Height,
    double FrameRate,
    int BitrateKbps);

/// <summary>Параметры аудиокодека профиля.</summary>
public sealed record CameraProfileAudio(
    string AudioCodec,
    int AudioSampleRate,
    int BitrateKbps);
