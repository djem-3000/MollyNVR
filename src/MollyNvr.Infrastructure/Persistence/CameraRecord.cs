namespace MollyNvr.Infrastructure.Persistence;

/// <summary>Строка таблицы камер; учётные данные хранятся зашифрованными.</summary>
public sealed class CameraRecord
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;

    public int RtspPort { get; set; }

    public string VendorKey { get; set; } = string.Empty;

    public string Vendor { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string Firmware { get; set; } = string.Empty;

    public string? SerialNumber { get; set; }

    /// <summary>Активна ли камера (неактивные не показываются в live-сетке).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Идентификатор основного профиля камеры.</summary>
    public string MainProfileId { get; set; } = string.Empty;

    /// <summary>Идентификатор субпотока; при отсутствии субпотоков совпадает с основным профилем.</summary>
    public string SubProfileId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Зашифрованный логин и пароль (AES-GCM).</summary>
    public string Credentials { get; set; } = string.Empty;

    public List<CameraProfileRecord> Profiles { get; set; } = [];
}
