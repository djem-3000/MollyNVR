namespace MollyNvr.Infrastructure.Discovery;

/// <summary>Camera discovery parameters.</summary>
public sealed class DiscoveryOptions
{
    public const string SectionName = "Discovery";

        /// <summary>Default range for the add-camera wizard.</summary>
    public string DefaultRange { get; set; } = "192.168.1.0/24";
}
