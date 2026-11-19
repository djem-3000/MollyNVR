namespace MollyNvr.Application.Archive;

/// <summary>Event type labels for archive filters (without contacting the camera).</summary>
public static class ArchiveEventLabels
{
    private static readonly IReadOnlyDictionary<string, string> Labels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
                        ["motion"] = "Motion",
                        ["linecrossing"] = "Line crossing",
                        ["intrusion"] = "Intrusion",
                        ["regionentrance"] = "Region entrance",
                        ["regionexiting"] = "Region exit",
                        ["loitering"] = "Loitering",
                        ["face"] = "Face",
                        ["person"] = "Person",
                        ["vehicle"] = "Vehicle",
                        ["tamper"] = "Tamper",
                        ["animal"] = "Animal",
                        ["sound"] = "Sound",
        };

        /// <summary>Event type label; an unknown key is returned as is.</summary>
    public static string Label(string key) =>
        Labels.TryGetValue(key, out var label) ? label : key;
}
