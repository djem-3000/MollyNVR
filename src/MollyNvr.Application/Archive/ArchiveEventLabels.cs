namespace MollyNvr.Application.Archive;

/// <summary>Подписи типов событий для фильтров архива (без обращения к камере).</summary>
public static class ArchiveEventLabels
{
    private static readonly IReadOnlyDictionary<string, string> Labels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["motion"] = "Движение",
            ["linecrossing"] = "Пересечение линии",
            ["intrusion"] = "Вторжение в зону",
            ["regionentrance"] = "Вход в область",
            ["regionexiting"] = "Выход из области",
            ["loitering"] = "Задержка в зоне",
            ["face"] = "Лицо",
            ["person"] = "Человек",
            ["vehicle"] = "Транспорт",
            ["tamper"] = "Вандализм",
            ["animal"] = "Животное",
            ["sound"] = "Звук",
        };

    /// <summary>Подпись типа события; неизвестный ключ возвращается как есть.</summary>
    public static string Label(string key) =>
        Labels.TryGetValue(key, out var label) ? label : key;
}
