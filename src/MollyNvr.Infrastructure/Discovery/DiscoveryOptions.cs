namespace MollyNvr.Infrastructure.Discovery;

/// <summary>Параметры автопоиска камер.</summary>
public sealed class DiscoveryOptions
{
    public const string SectionName = "Discovery";

    /// <summary>Диапазон по умолчанию для мастера добавления камеры.</summary>
    public string DefaultRange { get; set; } = "192.168.1.0/24";
}
