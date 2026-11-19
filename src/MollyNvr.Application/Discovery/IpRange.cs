using System.Net;

namespace MollyNvr.Application.Discovery;

/// <summary>IPv4 address range for camera discovery.</summary>
public sealed record IpRange(IPAddress First, IPAddress Last)
{
        /// <summary>Maximum number of addresses in one scan.</summary>
    public const int MaxHosts = 1024;

    /// <summary>
        /// Parses a single address, CIDR (<c>192.168.1.0/24</c>) or a range
    /// (<c>192.168.1.10-192.168.1.50</c>, <c>192.168.1.10-50</c>).
    /// </summary>
    public static IpRange Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
                        throw new ArgumentException("Specify an address range to search.", nameof(value));
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
                        throw new ArgumentException($"Invalid IPv4 address: '{text}'.", nameof(value));
        }

        return new IpRange(address, address);
    }

        /// <summary>All addresses of the range, inclusive (no more than <see cref="MaxHosts"/>).</summary>
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

        /// <summary>IPv4 address as a 32-bit number (for sorting and range arithmetic).</summary>
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
                        throw new ArgumentException($"Invalid CIDR range: '{text}'.", nameof(text));
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
                        throw new ArgumentException($"Invalid start address: '{startText}'.", nameof(text));
        }

        IPAddress end;
        if (endText.Contains('.'))
        {
            if (!TryParseIpv4(endText, out end))
            {
                                throw new ArgumentException($"Invalid end address: '{endText}'.", nameof(text));
            }
        }
        else
        {
            if (!byte.TryParse(endText, out var lastOctet))
            {
                                throw new ArgumentException($"Invalid end address: '{endText}'.", nameof(text));
            }

            var startBytes = start.GetAddressBytes();
            end = new IPAddress(new[] { startBytes[0], startBytes[1], startBytes[2], lastOctet });
        }

        var first = ToUInt32(start);
        var last = ToUInt32(end);
        if (last < first)
        {
                        throw new ArgumentException("The end of the range is earlier than the start.", nameof(text));
        }

        EnsureHostLimit((long)last - first + 1);
        return new IpRange(start, end);
    }

    private static void EnsureHostLimit(long count)
    {
        if (count > MaxHosts)
        {
                        throw new ArgumentException($"The range contains {count} addresses — the maximum is {MaxHosts}.");
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
