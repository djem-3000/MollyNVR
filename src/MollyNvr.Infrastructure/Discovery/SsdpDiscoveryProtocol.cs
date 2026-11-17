using System.Net;
using System.Text;
using System.Xml.Linq;
using MollyNvr.Application.Discovery;

namespace MollyNvr.Infrastructure.Discovery;

/// <summary>SSDP/UPnP: M-SEARCH на multicast 239.255.255.250:1900 и чтение описания устройства.</summary>
public sealed class SsdpDiscoveryProtocol(IHttpClientFactory httpClientFactory) : IDiscoveryProtocol
{
    private static readonly IPEndPoint Target = new(IPAddress.Parse("239.255.255.250"), 1900);
    private static readonly TimeSpan ListenTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DescriptionTimeout = TimeSpan.FromSeconds(2);

    private static readonly string[] CameraKeywords =
    [
        "camera",
        "nvr",
        "dvr",
        "onvif",
        "hikvision",
        "dahua",
        "reolink",
        "uniview",
        "amcrest",
        "foscam",
        "vivotek",
        "xiongmai",
        "ezviz",
        "tapo",
    ];

    public string Name => "SSDP/UPnP";

    public async Task<IReadOnlyList<DiscoveryResponse>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes(
            "M-SEARCH * HTTP/1.1\r\n" +
            "HOST: 239.255.255.250:1900\r\n" +
            "MAN: \"ssdp:discover\"\r\n" +
            "MX: 2\r\n" +
            "ST: ssdp:all\r\n\r\n");

        var responses = await UdpDiscovery.QueryAsync(
            payload,
            [Target],
            ListenTimeout,
            cancellationToken);

        var locations = responses
            .Select(static response => ParseHeaders(Encoding.UTF8.GetString(response.Data)))
            .Where(static headers => headers.ContainsKey("LOCATION"))
            .Select(static headers => headers["LOCATION"])
            .Where(static location => Uri.TryCreate(location, UriKind.Absolute, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (locations.Count == 0)
        {
            return [];
        }

        var descriptions = await Task.WhenAll(
            locations.Select(location => ReadDescriptionAsync(location, cancellationToken)));

        return descriptions
            .Where(static description => description is not null)
            .Select(static description => description!)
            .GroupBy(static description => description.Address)
            .Select(static group => group.First())
            .ToList();
    }

    private async Task<DiscoveryResponse?> ReadDescriptionAsync(
        string location,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(DescriptionTimeout);

        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = DescriptionTimeout;
            var xml = await client.GetStringAsync(location, timeoutSource.Token);
            var document = XDocument.Parse(xml);

            var manufacturer = FindValue(document, "manufacturer");
            var model = FindValue(document, "modelName");
            var friendlyName = FindValue(document, "friendlyName");
            var deviceType = FindValue(document, "deviceType");

            if (!LooksLikeCamera(manufacturer, model, friendlyName, deviceType) ||
                !Uri.TryCreate(location, UriKind.Absolute, out var uri) ||
                !IPAddress.TryParse(uri.Host, out var address))
            {
                return null;
            }

            return new DiscoveryResponse(
                Address: address,
                Vendor: manufacturer,
                Model: model ?? friendlyName);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool LooksLikeCamera(
        string? manufacturer,
        string? model,
        string? friendlyName,
        string? deviceType)
    {
        if (deviceType?.Contains("InternetGatewayDevice", StringComparison.OrdinalIgnoreCase) == true ||
            deviceType?.Contains("WANDevice", StringComparison.OrdinalIgnoreCase) == true)
        {
            return false;
        }

        var text = string.Join(' ', manufacturer, model, friendlyName, deviceType);
        return CameraKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string? FindValue(XDocument document, string localName) =>
        document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))?
            .Value;

    private static Dictionary<string, string> ParseHeaders(string payload)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in payload.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }

        return headers;
    }
}
