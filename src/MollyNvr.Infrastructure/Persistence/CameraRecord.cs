namespace MollyNvr.Infrastructure.Persistence;

/// <summary>Row of the cameras table; credentials are stored encrypted.</summary>
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

        /// <summary>Whether the camera is active (inactive cameras are hidden in the live grid).</summary>
    public bool IsActive { get; set; } = true;

        /// <summary>Identifier of the camera main profile.</summary>
    public string MainProfileId { get; set; } = string.Empty;

        /// <summary>Substream identifier; when there are no substreams it matches the main profile.</summary>
    public string SubProfileId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>Encrypted login and password (AES-GCM).</summary>
    public string Credentials { get; set; } = string.Empty;

    public List<CameraProfileRecord> Profiles { get; set; } = [];
}
