using System.Net;
using System.Text;
using System.Xml.Linq;
using MollyNvr.Application.Discovery;

namespace MollyNvr.Infrastructure.Discovery;

/// <summary>Hikvision SADP: XML Probe на UDP 37020 (multicast и broadcast).</summary>
public sealed class SadpDiscoveryProtocol : IDiscoveryProtocol
{
    private static readonly IReadOnlyList<IPEndPoint> Targets =
    [
        new(IPAddress.Parse("239.255.255.250"), 37020),
        new(IPAddress.Broadcast, 37020),
    ];

    private static readonly TimeSpan ListenTimeout = TimeSpan.FromSeconds(2);

    public string Name => "Hikvision SADP";

    public async Task<IReadOnlyList<DiscoveryResponse>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes(
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Probe>
              <Uuid>{Guid.NewGuid()}</Uuid>
              <Types>inquiry</Types>
            </Probe>
            """);

        var responses = await UdpDiscovery.QueryAsync(
            payload,
            Targets,
            ListenTimeout,
            cancellationToken);

        var devices = new Dictionary<IPAddress, DiscoveryResponse>();

        foreach (var (data, remote) in responses)
        {
            var description = Parse(data);
            if (description is null)
            {
                continue;
            }

            var address = description.Address ?? remote.Address;
            devices[address] = new DiscoveryResponse(
                Address: address,
                Mac: description.Mac,
                VendorKey: "hikvision",
                Vendor: "Hikvision",
                Model: description.Model);
        }

        return [.. devices.Values];
    }

    private static SadpDevice? Parse(byte[] payload)
    {
        try
        {
            var text = Encoding.UTF8.GetString(payload);
            var start = text.IndexOf('<');
            if (start < 0)
            {
                return null;
            }

            var document = XDocument.Parse(text[start..]);
            var description = document.Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "DeviceDescription");

            if (description is null)
            {
                return null;
            }

            var addressText = Value(description, "IPv4Address");
            var address = IPAddress.TryParse(addressText, out var parsed) ? parsed : null;
            var mac = Value(description, "MAC");
            var model = Value(description, "DeviceType") ?? Value(description, "DeviceDescription");

            return new SadpDevice(
                address,
                string.IsNullOrWhiteSpace(mac) ? null : mac,
                string.IsNullOrWhiteSpace(model) ? null : model);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private static string? Value(XElement parent, string localName) =>
        parent.Elements()
            .FirstOrDefault(element => element.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))?
            .Value;

    private sealed record SadpDevice(IPAddress? Address, string? Mac, string? Model);
}
