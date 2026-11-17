namespace MollyNvr.Infrastructure.Persistence;

/// <summary>Строка таблицы профилей потоков камеры.</summary>
public sealed class CameraProfileRecord
{
    public Guid CameraId { get; set; }

    public CameraRecord Camera { get; set; } = null!;

    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string VideoCodec { get; set; } = string.Empty;

    public int Width { get; set; }

    public int Height { get; set; }

    public double FrameRate { get; set; }

    public int BitrateKbps { get; set; }

    public string? AudioCodec { get; set; }

    public int? AudioSampleRate { get; set; }

    public int? AudioBitrateKbps { get; set; }

    public string RtspUrl { get; set; } = string.Empty;
}
