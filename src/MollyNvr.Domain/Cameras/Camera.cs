namespace MollyNvr.Domain.Cameras;

/// <summary>Camera in the registry. Credentials are not stored in the domain — they are encrypted in the infrastructure.</summary>
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
                        throw new ArgumentException("The camera identifier cannot be empty.", nameof(id));
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

        /// <summary>Whether the camera is active: inactive cameras are hidden in the live grid.</summary>
    public bool IsActive { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }

    public IReadOnlyList<CameraProfile> Profiles { get; }

        /// <summary>Identifier of the camera main profile.</summary>
    public string MainProfileId { get; }

    /// <summary>
        /// Substream identifier for the live grid. When there are no substreams, it matches <see cref="MainProfileId"/>.
    /// </summary>
    public string SubProfileId { get; }

        /// <summary>Creates a new camera with a new identifier.</summary>
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

        /// <summary>Returns an updated copy of the camera (identifier and creation date are preserved).</summary>
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

        /// <summary>Returns a copy of the camera with a changed name and selected substream.</summary>
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

        /// <summary>Returns a copy of the camera with a new active flag.</summary>
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
                        throw new ArgumentException("The camera main profile is not specified.", nameof(mainProfileId));
        }

        return profiles.Any(profile => string.Equals(profile.Id, mainProfileId, StringComparison.Ordinal))
            ? mainProfileId
            : throw new ArgumentException(
                                $"Main profile '{mainProfileId}' was not found on the camera.",
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
                                        $"Substream '{subProfileId}' was not found on the camera.",
                    nameof(subProfileId));
        }

        return profiles.FirstOrDefault(profile =>
            !string.Equals(profile.Id, mainProfileId, StringComparison.Ordinal))?.Id ?? mainProfileId;
    }

    private static string ValidateText(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
                        throw new ArgumentException("The value cannot be empty.", parameterName);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
                        throw new ArgumentException($"The value is longer than {maxLength} characters.", parameterName);
        }

        return trimmed;
    }

    private static int ValidatePort(int port, string parameterName) =>
        port is > 0 and <= 65535
            ? port
                        : throw new ArgumentOutOfRangeException(parameterName, port, "The port must be in the range 1-65535.");
}
