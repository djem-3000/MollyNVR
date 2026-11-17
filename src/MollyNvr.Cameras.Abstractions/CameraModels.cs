namespace MollyNvr.Cameras.Abstractions;

/// <summary>Сведения об устройстве, полученные модулем производителя.</summary>
public sealed record CameraDeviceInfo(
    string Vendor,
    string Model,
    string Firmware,
    string? SerialNumber,
    string? DeviceName);

/// <summary>Назначение профиля потока.</summary>
public enum CameraStreamProfileKind
{
    /// <summary>Основной поток — максимальное разрешение.</summary>
    Main = 0,

    /// <summary>Субпоток — низкое разрешение для сетки тайлов.</summary>
    Sub = 1,
}

/// <summary>Параметры видеокодека профиля.</summary>
public sealed record CameraVideoSettings(
    string Codec,
    int Width,
    int Height,
    double FrameRate,
    int BitrateKbps);

/// <summary>Параметры аудиокодека профиля.</summary>
public sealed record CameraAudioSettings(bool Enabled, string? Codec, int? SampleRate, int? Bitrate);

/// <summary>Профиль потока камеры (основной или субпоток).</summary>
public sealed record CameraProfile(
    CameraStreamProfileKind Kind,
    string Id,
    string? Name,
    CameraVideoSettings Video,
    CameraAudioSettings Audio,
    string RtspUrl)
{
    public string DisplayName => Name ?? (Kind == CameraStreamProfileKind.Main ? "Основной поток" : "Субпоток");
}

/// <summary>Результат опроса камеры модулем производителя.</summary>
public sealed record CameraProbeResult(
    CameraDeviceInfo Device,
    IReadOnlyList<CameraProfile> Profiles)
{
    public CameraProfile? MainProfile =>
        Profiles.FirstOrDefault(static profile => profile.Kind == CameraStreamProfileKind.Main);

    public CameraProfile? SubProfile =>
        Profiles.FirstOrDefault(static profile => profile.Kind == CameraStreamProfileKind.Sub);
}
