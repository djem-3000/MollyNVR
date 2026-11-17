namespace MollyNvr.Domain.Cameras;

/// <summary>Камера в реестре. Учётные данные в Domain не хранятся — они шифруются в инфраструктуре.</summary>
public sealed record Camera
{
    public const int NameMaxLength = 100;
    public const int VendorKeyMaxLength = 32;
    public const int HostMaxLength = 253;

    public Camera(
        Guid id,
        string name,
        string host,
        int rtspPort,
        string vendorKey,
        string vendor,
        string model,
        string firmware,
        string? serialNumber,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        IReadOnlyList<CameraProfile> profiles,
        string mainProfileId,
        string? subProfileId = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор камеры не может быть пустым.", nameof(id));
        }

        Id = id;
        Name = ValidateText(name, NameMaxLength, nameof(name));
        Host = ValidateText(host, HostMaxLength, nameof(host));
        RtspPort = ValidatePort(rtspPort, nameof(rtspPort));
        VendorKey = ValidateText(vendorKey, VendorKeyMaxLength, nameof(vendorKey)).ToLowerInvariant();
        Vendor = ValidateText(vendor, NameMaxLength, nameof(vendor));
        Model = ValidateText(model, NameMaxLength, nameof(model));
        Firmware = firmware ?? string.Empty;
        SerialNumber = serialNumber;
        IsActive = isActive;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        MainProfileId = ResolveMainProfileId(profiles, mainProfileId);
        SubProfileId = ResolveSubProfileId(profiles, MainProfileId, subProfileId);
    }

    public Guid Id { get; }

    public string Name { get; }

    public string Host { get; }

    public int RtspPort { get; }

    public string VendorKey { get; }

    public string Vendor { get; }

    public string Model { get; }

    public string Firmware { get; }

    public string? SerialNumber { get; }

    /// <summary>Активна ли камера: неактивные не показываются в live-сетке.</summary>
    public bool IsActive { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }

    public IReadOnlyList<CameraProfile> Profiles { get; }

    /// <summary>Идентификатор основного профиля камеры.</summary>
    public string MainProfileId { get; }

    /// <summary>
    /// Идентификатор субпотока для live-сетки. Если субпотоков нет, совпадает с <see cref="MainProfileId"/>.
    /// </summary>
    public string SubProfileId { get; }

    /// <summary>Создаёт новую камеру с новым идентификатором.</summary>
    public static Camera Register(
        string name,
        string host,
        int rtspPort,
        string vendorKey,
        string vendor,
        string model,
        string firmware,
        string? serialNumber,
        IReadOnlyList<CameraProfile> profiles,
        string mainProfileId,
        DateTimeOffset now,
        string? subProfileId = null) =>
        new(
            Guid.NewGuid(),
            name,
            host,
            rtspPort,
            vendorKey,
            vendor,
            model,
            firmware,
            serialNumber,
            isActive: true,
            createdAt: now,
            updatedAt: now,
            profiles,
            mainProfileId,
            subProfileId);

    /// <summary>Возвращает обновлённую копию камеры (идентификатор и дата создания сохраняются).</summary>
    public Camera Update(
        string name,
        string host,
        int rtspPort,
        string vendorKey,
        string vendor,
        string model,
        string firmware,
        string? serialNumber,
        IReadOnlyList<CameraProfile> profiles,
        string mainProfileId,
        DateTimeOffset now,
        string? subProfileId = null) =>
        new(
            Id,
            name,
            host,
            rtspPort,
            vendorKey,
            vendor,
            model,
            firmware,
            serialNumber,
            IsActive,
            CreatedAt,
            now,
            profiles,
            mainProfileId,
            subProfileId);

    /// <summary>Возвращает копию камеры с изменёнными именем и выбранным субпотоком.</summary>
    public Camera UpdateSettings(string name, string? subProfileId, DateTimeOffset now) =>
        new(
            Id,
            name,
            Host,
            RtspPort,
            VendorKey,
            Vendor,
            Model,
            Firmware,
            SerialNumber,
            IsActive,
            CreatedAt,
            now,
            Profiles,
            MainProfileId,
            subProfileId);

    /// <summary>Возвращает копию камеры с новым признаком активности.</summary>
    public Camera SetActive(bool isActive, DateTimeOffset now) =>
        new(
            Id,
            Name,
            Host,
            RtspPort,
            VendorKey,
            Vendor,
            Model,
            Firmware,
            SerialNumber,
            isActive,
            CreatedAt,
            now,
            Profiles,
            MainProfileId,
            SubProfileId);

    private static string ResolveMainProfileId(
        IReadOnlyList<CameraProfile> profiles,
        string mainProfileId)
    {
        if (string.IsNullOrWhiteSpace(mainProfileId))
        {
            throw new ArgumentException("Не указан основной профиль камеры.", nameof(mainProfileId));
        }

        return profiles.Any(profile => string.Equals(profile.Id, mainProfileId, StringComparison.Ordinal))
            ? mainProfileId
            : throw new ArgumentException(
                $"Основной профиль «{mainProfileId}» не найден у камеры.",
                nameof(mainProfileId));
    }

    private static string ResolveSubProfileId(
        IReadOnlyList<CameraProfile> profiles,
        string mainProfileId,
        string? subProfileId)
    {
        if (!string.IsNullOrWhiteSpace(subProfileId) &&
            !string.Equals(subProfileId, mainProfileId, StringComparison.Ordinal))
        {
            return profiles.Any(profile => string.Equals(profile.Id, subProfileId, StringComparison.Ordinal))
                ? subProfileId
                : throw new ArgumentException(
                    $"Субпоток «{subProfileId}» не найден у камеры.",
                    nameof(subProfileId));
        }

        return profiles.FirstOrDefault(profile =>
            !string.Equals(profile.Id, mainProfileId, StringComparison.Ordinal))?.Id ?? mainProfileId;
    }

    private static string ValidateText(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Значение не может быть пустым.", parameterName);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Значение длиннее {maxLength} символов.", parameterName);
        }

        return trimmed;
    }

    private static int ValidatePort(int port, string parameterName) =>
        port is > 0 and <= 65535
            ? port
            : throw new ArgumentOutOfRangeException(parameterName, port, "Порт должен быть в диапазоне 1–65535.");
}
