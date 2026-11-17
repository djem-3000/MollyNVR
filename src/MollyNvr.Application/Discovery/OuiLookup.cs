using System.Reflection;

namespace MollyNvr.Application.Discovery;

/// <summary>Справочник OUI камер и сетевого оборудования (подмножество IEEE oui.csv).</summary>
public sealed class OuiLookup : IOuiLookup
{
    private const string ResourceName = "MollyNvr.Application.Discovery.oui-cameras.csv";

    private static readonly Lazy<IReadOnlyDictionary<string, string>> Vendors = new(Load);

    public string? FindVendor(string? macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
        {
            return null;
        }

        var normalized = Normalize(macAddress);
        return normalized.Length < 6 ? null : Vendors.Value.GetValueOrDefault(normalized[..6]);
    }

    private static string Normalize(string value)
    {
        var chars = new List<char>(value.Length);

        foreach (var symbol in value)
        {
            if (Uri.IsHexDigit(symbol))
            {
                chars.Add(char.ToUpperInvariant(symbol));
            }
        }

        return new string([.. chars]);
    }

    private static IReadOnlyDictionary<string, string> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Не найден встроенный ресурс OUI: {ResourceName}.");

        using var reader = new StreamReader(stream);
        var vendors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var separator = line.IndexOf(',');
            if (separator <= 0 || separator == line.Length - 1)
            {
                continue;
            }

            vendors[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }

        return vendors;
    }
}
