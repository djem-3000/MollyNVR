using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MollyNvr.Application.Discovery;

namespace MollyNvr.Infrastructure.Discovery;

/// <summary>mDNS/Bonjour: запросы PTR для _rtsp/_onvif/_http и разбор A-записей из ответов.</summary>
public sealed class MdnsDiscoveryProtocol : IDiscoveryProtocol
{
    private static readonly IPAddress MulticastAddress = IPAddress.Parse("224.0.0.251");
    private const int MdnsPort = 5353;
    private static readonly TimeSpan ListenTimeout = TimeSpan.FromSeconds(2);
    private static readonly string[] Services = ["_rtsp._tcp.local", "_onvif._tcp.local", "_http._tcp.local"];

    public string Name => "mDNS/Bonjour";

    public async Task<IReadOnlyList<DiscoveryResponse>> DiscoverAsync(CancellationToken cancellationToken)
    {
        using var client = new UdpClient(AddressFamily.InterNetwork);
        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        client.Client.Bind(new IPEndPoint(IPAddress.Any, MdnsPort));

        if (MulticastInterface.FindLanAddress() is { } lanAddress)
        {
            client.JoinMulticastGroup(MulticastAddress, lanAddress);
        }
        else
        {
            client.JoinMulticastGroup(MulticastAddress);
        }

        var payload = CreateQuery();
        await client.SendAsync(payload, new IPEndPoint(MulticastAddress, MdnsPort), cancellationToken);

        var responses = await UdpDiscovery.ReceiveAsync(client, ListenTimeout, cancellationToken);
        var devices = new Dictionary<IPAddress, DiscoveryResponse>();

        foreach (var (data, remote) in responses)
        {
            var result = Parse(data);
            if (result is null)
            {
                continue;
            }

            foreach (var address in result.Addresses)
            {
                devices.TryAdd(
                    address,
                    new DiscoveryResponse(Address: address, Model: result.Name));
            }
        }

        return [.. devices.Values];
    }

    private static byte[] CreateQuery()
    {
        using var stream = new MemoryStream();
        Span<byte> header = stackalloc byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(header[4..], (ushort)Services.Length);
        stream.Write(header);

        Span<byte> question = stackalloc byte[4];
        foreach (var service in Services)
        {
            WriteName(stream, service);
            BinaryPrimitives.WriteUInt16BigEndian(question, 12);
            BinaryPrimitives.WriteUInt16BigEndian(question[2..], 1);
            stream.Write(question);
        }

        return stream.ToArray();
    }

    private static void WriteName(Stream stream, string name)
    {
        foreach (var label in name.Split('.'))
        {
            var bytes = Encoding.ASCII.GetBytes(label);
            stream.WriteByte((byte)bytes.Length);
            stream.Write(bytes);
        }

        stream.WriteByte(0);
    }

    private static MdnsResult? Parse(byte[] data)
    {
        if (data.Length < 12)
        {
            return null;
        }

        var questions = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4));
        var answers = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(6));
        var authority = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(8));
        var additional = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(10));

        var offset = 12;
        for (var index = 0; index < questions; index++)
        {
            if (ReadName(data, ref offset) is null || offset + 4 > data.Length)
            {
                return null;
            }

            offset += 4;
        }

        var addresses = new List<IPAddress>();
        string? serviceName = null;

        for (var index = 0; index < answers + authority + additional; index++)
        {
            var owner = ReadName(data, ref offset);
            if (owner is null || offset + 10 > data.Length)
            {
                break;
            }

            var type = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset));
            var length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 8));
            var dataOffset = offset + 10;
            var dataStart = dataOffset;
            var nextOffset = dataStart + length;

            if (nextOffset > data.Length)
            {
                break;
            }

            if (type == 12)
            {
                var target = ReadName(data, ref dataOffset);
                serviceName ??= TargetName(target);
            }
            else if (type == 1 && length == 4)
            {
                addresses.Add(new IPAddress(data.AsSpan(dataStart, 4)));
            }

            offset = nextOffset;
        }

        return serviceName is null || addresses.Count == 0
            ? null
            : new MdnsResult(addresses, serviceName);
    }

    private static string? TargetName(string? target)
    {
        if (string.IsNullOrWhiteSpace(target) ||
            !(target.Contains("_rtsp", StringComparison.OrdinalIgnoreCase) ||
              target.Contains("_onvif", StringComparison.OrdinalIgnoreCase) ||
              target.Contains("_http", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var label = target.Split('.')[0];
        return string.IsNullOrWhiteSpace(label) ? null : label;
    }

    private static string? ReadName(byte[] data, ref int offset)
    {
        var labels = new List<string>();
        var position = offset;
        var jumped = false;
        var safety = 0;

        while (position < data.Length && safety++ < 32)
        {
            var length = data[position];

            if (length == 0)
            {
                if (!jumped)
                {
                    offset = position + 1;
                }

                return string.Join('.', labels);
            }

            if ((length & 0xC0) == 0xC0)
            {
                if (position + 1 >= data.Length)
                {
                    return null;
                }

                var pointer = ((length & 0x3F) << 8) | data[position + 1];
                if (!jumped)
                {
                    offset = position + 2;
                }

                position = pointer;
                jumped = true;
                continue;
            }

            position++;
            if (position + length > data.Length)
            {
                return null;
            }

            labels.Add(Encoding.ASCII.GetString(data, position, length));
            position += length;
        }

        return null;
    }

    private sealed record MdnsResult(IReadOnlyList<IPAddress> Addresses, string? Name);
}
