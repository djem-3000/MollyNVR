namespace MollyNvr.Cameras.Reolink;

/// <summary>
/// Параметры моста neolink.net: он говорит с камерой по Baichuan (порт 9000)
/// и отдаёт обычный RTSP с HTTP-API.
/// </summary>
public sealed record ReolinkBridgeOptions
{
    /// <summary>Базовый адрес HTTP-API моста (со слэшем на конце).</summary>
    public required Uri ApiBaseUrl { get; init; }

    /// <summary>Базовый адрес RTSP моста; к нему добавляется имя камеры и поток.</summary>
    public required Uri RtspBaseUrl { get; init; }

    /// <summary>Файл конфигурации моста; его изменение перезапускает мост.</summary>
    public required string ConfigPath { get; init; }

    /// <summary>Сколько ждать появления камеры в сети после записи конфигурации.</summary>
    public TimeSpan OnlineTimeout { get; init; } = TimeSpan.FromSeconds(35);
}
