using System.Net;

namespace MollyNvr.Application.Discovery;

/// <summary>Диапазон IPv4-адресов для автопоиска камер.</summary>
public sealed record IpRange(IPAddress First, IPAddress Last)
{
    /// <summary>Максимальное число адресов в одном сканировании.</summary>
    public const int MaxHosts = 1024;

    /// <summary>
    /// Разбирает одиночный адрес, CIDR (<c>192.168.1.0/24</c>) или диапазон
    /// (<c>192.168.1.10-192.168.1.50</c>, <c>192.168.1.10-50</c>).
    /// </summary>
    public static IpRange Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Укажите диапазон адресов для поиска.", nameof(value));
        }

        var text = value.Trim();

        if (text.Contains('/'))
        {
            return ParseCidr(text);
        }

        if (text.Contains('-'))
        {
            return ParseExplicitRange(text);
        }

        if (!TryParseIpv4(text, out var address))
        {
            throw new ArgumentException($"Некорректный IPv4-адрес: «{text}».", nameof(value));
        }

        return new IpRange(address, address);
    }

    /// <summary>Все адреса диапазона включительно (не больше <see cref="MaxHosts"/>).</summary>
    public IReadOnlyList<IPAddress> Hosts()
    {
        var first = ToUInt32(First);
        var last = ToUInt32(Last);
        var count = (long)last - first + 1;
        var hosts = new List<IPAddress>((int)count);

        for (var current = first; current <= last; current++)
        {
            hosts.Add(FromUInt32(current));

            if (current == uint.MaxValue)
            {
                break;
            }
        }

        return hosts;
    }

    /// <summary>IPv4-адрес как 32-битное число (для сортировки и арифметики диапазонов).</summary>
    public static uint ToUInt32(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }

    private static IpRange ParseCidr(string text)
    {
        var parts = text.Split('/', 2);
        if (!TryParseIpv4(parts[0].Trim(), out var address) ||
            !int.TryParse(parts[1].Trim(), out var prefix) ||
            prefix is < 8 or > 32)
        {
            throw new ArgumentException($"Некорректный CIDR-диапазон: «{text}».", nameof(text));
        }

        var mask = prefix == 32 ? uint.MaxValue : uint.MaxValue << (32 - prefix);
        var network = ToUInt32(address) & mask;
        var broadcast = network | ~mask;
        EnsureHostLimit((long)broadcast - network + 1);

        return new IpRange(FromUInt32(network), FromUInt32(broadcast));
    }

    private static IpRange ParseExplicitRange(string text)
    {
        var parts = text.Split('-', 2);
        var startText = parts[0].Trim();
        var endText = parts[1].Trim();

        if (!TryParseIpv4(startText, out var start))
        {
            throw new ArgumentException($"Некорректный начальный адрес: «{startText}».", nameof(text));
        }

        IPAddress end;
        if (endText.Contains('.'))
        {
            if (!TryParseIpv4(endText, out end))
            {
                throw new ArgumentException($"Некорректный конечный адрес: «{endText}».", nameof(text));
            }
        }
        else
        {
            if (!byte.TryParse(endText, out var lastOctet))
            {
                throw new ArgumentException($"Некорректный конечный адрес: «{endText}».", nameof(text));
            }

            var startBytes = start.GetAddressBytes();
            end = new IPAddress(new[] { startBytes[0], startBytes[1], startBytes[2], lastOctet });
        }

        var first = ToUInt32(start);
        var last = ToUInt32(end);
        if (last < first)
        {
            throw new ArgumentException("Конец диапазона раньше начала.", nameof(text));
        }

        EnsureHostLimit((long)last - first + 1);
        return new IpRange(start, end);
    }

    private static void EnsureHostLimit(long count)
    {
        if (count > MaxHosts)
        {
            throw new ArgumentException($"Диапазон содержит {count} адресов — максимум {MaxHosts}.");
        }
    }

    private static bool TryParseIpv4(string value, out IPAddress address)
    {
        if (IPAddress.TryParse(value, out var parsed) && parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            address = parsed;
            return true;
        }

        address = IPAddress.None;
        return false;
    }

    private static IPAddress FromUInt32(uint value) =>
        new(new[]
        {
            (byte)(value >> 24),
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)value,
        });
}
