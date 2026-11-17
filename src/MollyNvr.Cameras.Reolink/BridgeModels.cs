namespace MollyNvr.Cameras.Reolink;

/// <summary>Мост neolink.net недоступен или его конфигурация не применилась.</summary>
public sealed class ReolinkBridgeException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>Ответы HTTP-API моста neolink.net (поля — как в его JSON).</summary>
internal sealed record BridgeCamera(string? Name, bool Online);

internal sealed record BridgeStreamInfo(IReadOnlyList<BridgeStreamProfile>? Profiles);

internal sealed record BridgeStreamProfile(
    string? Type,
    int Width,
    int Height,
    double DefaultFramerate,
    int DefaultBitrate);

internal sealed record BridgeCapabilities(BridgeVersion? Version);

internal sealed record BridgeVersion(string? Name, string? Model, string? Serial, string? Firmware);
