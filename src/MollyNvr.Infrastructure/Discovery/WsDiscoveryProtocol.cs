using System.Net;
using System.Text;
using System.Xml.Linq;
using MollyNvr.Application.Discovery;

namespace MollyNvr.Infrastructure.Discovery;

/// <summary>WS-Discovery (ONVIF): SOAP Probe на multicast 239.255.255.250:3702.</summary>
public sealed class WsDiscoveryProtocol : IDiscoveryProtocol
{
    private static readonly IPEndPoint Target = new(IPAddress.Parse("239.255.255.250"), 3702);
    private static readonly TimeSpan ListenTimeout = TimeSpan.FromSeconds(2);

    public string Name => "WS-Discovery";

    public async Task<IReadOnlyList<DiscoveryResponse>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes(CreateProbe());
        var responses = await UdpDiscovery.QueryAsync(
            payload,
            [Target],
            ListenTimeout,
            cancellationToken);

        var devices = new Dictionary<IPAddress, DiscoveryResponse>();

        foreach (var (data, remote) in responses)
        {
            var address = ParseAddress(data, remote.Address);
            if (address is null || devices.ContainsKey(address))
            {
                continue;
            }

            devices[address] = new DiscoveryResponse(
                Address: address,
                VendorKey: "onvif",
                Vendor: "ONVIF");
        }

        return [.. devices.Values];
    }

    private static string CreateProbe() =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope"
                    xmlns:a="http://schemas.xmlsoap.org/ws/2004/08/addressing"
                    xmlns:d="http://schemas.xmlsoap.org/ws/2005/04/discovery"
                    xmlns:dn="http://www.onvif.org/ver10/network/wsdl">
          <s:Header>
            <a:Action s:mustUnderstand="1">http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe</a:Action>
            <a:MessageID>urn:uuid:{Guid.NewGuid()}</a:MessageID>
            <a:To s:mustUnderstand="1">urn:schemas-xmlsoap-org:ws:2005:04:discovery</a:To>
          </s:Header>
          <s:Body>
            <d:Probe>
              <d:Types>dn:NetworkVideoTransmitter</d:Types>
            </d:Probe>
          </s:Body>
        </s:Envelope>
        """;

    private static IPAddress? ParseAddress(byte[] payload, IPAddress fallback)
    {
        var addrs = ParseText(payload, "XAddrs");

        foreach (var value in addrs.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                IPAddress.TryParse(uri.Host, out var address))
            {
                return address;
            }
        }

        return fallback;
    }

    private static string ParseText(byte[] payload, string localName)
    {
        try
        {
            var document = XDocument.Parse(Encoding.UTF8.GetString(payload));
            return document.Descendants()
                .FirstOrDefault(element => element.Name.LocalName == localName)?
                .Value ?? string.Empty;
        }
        catch (System.Xml.XmlException)
        {
            return string.Empty;
        }
    }
}
