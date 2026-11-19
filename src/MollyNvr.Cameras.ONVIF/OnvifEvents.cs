using System.Runtime.CompilerServices;
using System.Xml.Linq;
using MollyNvr.Cameras.Abstractions;
using MollyNvr.Cameras.Abstractions.Http;

namespace MollyNvr.Cameras.ONVIF;

/// <summary>ONVIF events: GetEventProperties + PullPoint subscription.</summary>
internal static class OnvifEvents
{
    private const string GetEventPropertiesAction = "http://www.onvif.org/ver10/events/wsdl/GetEventProperties";
    private const string CreateSubscriptionAction = "http://www.onvif.org/ver10/events/wsdl/CreatePullPointSubscription";
    private const string PullMessagesAction = "http://www.onvif.org/ver10/events/wsdl/PullMessages";

    private static readonly XNamespace Tev = "http://www.onvif.org/ver10/events/wsdl";
    private static readonly TimeSpan PullTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan SubscriptionLifetime = TimeSpan.FromMinutes(30);

    private static readonly string[] KeyOrder =
    [
        "motion", "person", "vehicle", "animal", "face", "linecrossing",
        "intrusion", "regionentrance", "regionexiting", "loitering", "tamper", "sound",
    ];

        /// <summary>Event type keys the camera can report.</summary>
    public static async Task<IReadOnlyList<CameraEventType>> GetEventTypesAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CancellationToken cancellationToken)
    {
        var serviceUri = await ResolveServiceUriAsync(endpoint, credentials, cancellationToken);
        if (serviceUri is null)
        {
            return [];
        }

        using var client = CreateClient(serviceUri);
        var body = await OnvifClient.SendAsync(
            client,
            serviceUri,
            GetEventPropertiesAction,
            new XElement(Tev + "GetEventProperties"),
            credentials,
            cancellationToken);

        if (OnvifSoap.ReadFault(body) is not null)
        {
            return [];
        }

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var document = XDocument.Parse(body);

            foreach (var topic in document.Descendants().Where(static element => element.Name.LocalName == "TopicSet"))
            {
                foreach (var path in LeafPaths(topic))
                {
                    var key = ToKey(path);

                    if (key is not null)
                    {
                        keys.Add(key);
                    }
                }
            }
        }
        catch (System.Xml.XmlException)
        {
            return [];
        }

        if (keys.Count > 0)
        {
                        // Reolink and some other firmware send MyRuleDetector AI events
                        // without declaring them in TopicSet — add the standard set.
            keys.Add("face");
            keys.Add("person");
            keys.Add("vehicle");
            keys.Add("animal");
        }

        return keys
            .OrderBy(static key =>
            {
                var index = Array.IndexOf(KeyOrder, key);
                return index < 0 ? int.MaxValue : index;
            })
            .Select(CreateType)
            .ToList();
    }

        /// <summary>Camera event stream through a PullPoint subscription.</summary>
    public static async IAsyncEnumerable<CameraEvent> WatchAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var serviceUri = await ResolveServiceUriAsync(endpoint, credentials, cancellationToken)
                        ?? throw new CameraOperationNotSupportedException("ONVIF did not report an event service.");

        using var client = CreateClient(serviceUri);
        var subscriptionUri = await CreateSubscriptionAsync(client, serviceUri, credentials, cancellationToken);
        var deadline = DateTimeOffset.UtcNow + SubscriptionLifetime;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                subscriptionUri = await CreateSubscriptionAsync(client, serviceUri, credentials, cancellationToken);
                deadline = DateTimeOffset.UtcNow + SubscriptionLifetime;
            }

            var body = await OnvifClient.SendAsync(
                client,
                subscriptionUri,
                PullMessagesAction,
                new XElement(
                    Tev + "PullMessages",
                    new XElement(Tev + "Timeout", "PT20S"),
                    new XElement(Tev + "MessageLimit", 20)),
                credentials,
                cancellationToken);

            var fault = OnvifSoap.ReadFault(body);

            if (fault is not null)
            {
                if (OnvifClient.IsAuthorizationFault(fault))
                {
                                        throw new CameraAuthenticationException("ONVIF rejected the credentials when subscribing to events.");
                }

                                // The subscription expired or the device lost it — create it again.
                subscriptionUri = await CreateSubscriptionAsync(client, serviceUri, credentials, cancellationToken);
                deadline = DateTimeOffset.UtcNow + SubscriptionLifetime;
                continue;
            }

            foreach (var cameraEvent in ParseNotifications(body))
            {
                yield return cameraEvent;
            }
        }
    }

    private static async Task<Uri?> ResolveServiceUriAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(endpoint.OnvifServiceUri);

        try
        {
            var body = await OnvifClient.SendAsync(
                client,
                endpoint.OnvifServiceUri,
                OnvifSoap.Actions.GetCapabilities,
                OnvifSoap.CreateGetCapabilities("Events"),
                credentials,
                cancellationToken);

            if (OnvifSoap.ReadFault(body) is not null)
            {
                return null;
            }

            var document = XDocument.Parse(body);
            var address = document.Descendants()
                .FirstOrDefault(static element => element.Name.LocalName == "Events")?
                .Descendants()
                .FirstOrDefault(static element => element.Name.LocalName == "XAddr")?
                .Value;

            return Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or System.Xml.XmlException)
        {
            return null;
        }
    }

    private static async Task<Uri> CreateSubscriptionAsync(
        HttpClient client,
        Uri serviceUri,
        CameraCredentials credentials,
        CancellationToken cancellationToken)
    {
                // Cameras treat InitialTerminationTime differently:
                // some expect a duration, others a point in time, and some do not expect the field at all.
        var attempts = new[]
        {
            new XElement(
                Tev + "CreatePullPointSubscription",
                new XElement(Tev + "InitialTerminationTime", "PT1H")),
            new XElement(Tev + "CreatePullPointSubscription"),
            new XElement(
                Tev + "CreatePullPointSubscription",
                new XElement(
                    Tev + "InitialTerminationTime",
                    DateTime.UtcNow.AddHours(1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"))),
        };

        string? lastResponse = null;

        foreach (var body in attempts)
        {
            var response = await OnvifClient.SendAsync(
                client,
                serviceUri,
                CreateSubscriptionAction,
                body,
                credentials,
                cancellationToken);

            if (OnvifSoap.ReadFault(response) is not null)
            {
                lastResponse = response;
                continue;
            }

            var document = XDocument.Parse(response);
            var address = document.Descendants()
                .FirstOrDefault(static element => element.Name.LocalName == "SubscriptionReference")?
                .Descendants()
                .FirstOrDefault(static element => element.Name.LocalName == "Address")?
                .Value;

            if (Uri.TryCreate(address, UriKind.Absolute, out var uri))
            {
                return uri;
            }

            lastResponse = response;
        }

        throw new CameraOperationNotSupportedException(
                        $"ONVIF did not create an event subscription. Response: {Shorten(lastResponse)}");
    }

    private static string Shorten(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
                        return "empty";
        }

        try
        {
            var document = XDocument.Parse(value);
            value = document.Descendants()
                .FirstOrDefault(static element => element.Name.LocalName == "Body")?
                .Value ?? value;
        }
        catch (System.Xml.XmlException)
        {
                        // not XML — keep as is
        }

        var text = value.Trim().ReplaceLineEndings(" ");
        return text.Length <= 400 ? text : text[..400] + "…";
    }

    private static IEnumerable<CameraEvent> ParseNotifications(string body)
    {
        XDocument document;

        try
        {
            document = XDocument.Parse(body);
        }
        catch (System.Xml.XmlException)
        {
            yield break;
        }

        foreach (var message in document.Descendants().Where(static element => element.Name.LocalName == "NotificationMessage"))
        {
            var topic = message.Descendants()
                .FirstOrDefault(static element => element.Name.LocalName == "Topic")?
                .Value;

            var key = ToKey(topic);
            if (key is null)
            {
                continue;
            }

            var active = ReadActive(message);
            var timestamp = ReadTimestamp(message);

            yield return new CameraEvent(key, timestamp, active);
        }
    }

    private static bool ReadActive(XElement message)
    {
        var item = message.Descendants()
            .FirstOrDefault(static element =>
                element.Name.LocalName == "SimpleItem" &&
                element.Attribute("Name") is { } name &&
                (name.Value.Contains("Motion", StringComparison.OrdinalIgnoreCase) ||
                 name.Value.Contains("State", StringComparison.OrdinalIgnoreCase) ||
                 name.Value.Contains("Value", StringComparison.OrdinalIgnoreCase)));

        var value = item?.Attribute("Value")?.Value ?? item?.Value;

        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return value is null || !value.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTimeOffset ReadTimestamp(XElement message)
    {
        var item = message.Descendants().FirstOrDefault(static element => element.Name.LocalName == "Message");
        var text = item?.Attribute("UtcTime")?.Value;

        return DateTimeOffset.TryParse(text, out var parsed) ? parsed : DateTimeOffset.Now;
    }

    private static IEnumerable<string> LeafPaths(XElement root)
    {
        foreach (var element in root.Descendants())
        {
            if (element.HasElements)
            {
                continue;
            }

            var parts = new List<string>();
            var current = element;

            while (current is not null && current != root)
            {
                parts.Insert(0, current.Name.LocalName);
                current = current.Parent;
            }

            if (parts.Count > 0)
            {
                yield return string.Join('/', parts);
            }
        }
    }

        /// <summary>Maps a topic/event type to an application key.</summary>
    internal static string? ToKey(string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return null;
        }

        var value = topic.ToLowerInvariant();

        if (value.Contains("people", StringComparison.Ordinal) || value.Contains("human", StringComparison.Ordinal) ||
            value.Contains("person", StringComparison.Ordinal))
        {
            return "person";
        }

        if (value.Contains("vehicle", StringComparison.Ordinal) || value.Contains("car", StringComparison.Ordinal))
        {
            return "vehicle";
        }

        if (value.Contains("animal", StringComparison.Ordinal) ||
            value.Contains("dog", StringComparison.Ordinal) ||
            value.Contains("cat", StringComparison.Ordinal) ||
            value.Contains("pet", StringComparison.Ordinal))
        {
            return "animal";
        }

        if (value.Contains("face", StringComparison.Ordinal))
        {
            return "face";
        }

        if (value.Contains("tamper", StringComparison.Ordinal))
        {
            return "tamper";
        }

        if (value.Contains("line", StringComparison.Ordinal))
        {
            return "linecrossing";
        }

        if (value.Contains("field", StringComparison.Ordinal) || value.Contains("intrusion", StringComparison.Ordinal))
        {
            return "intrusion";
        }

        if (value.Contains("loiter", StringComparison.Ordinal))
        {
            return "loitering";
        }

        if (value.Contains("sound", StringComparison.Ordinal) || value.Contains("audio", StringComparison.Ordinal))
        {
            return "sound";
        }

        if (value.Contains("motion", StringComparison.Ordinal) || value.Contains("cellmotion", StringComparison.Ordinal))
        {
            return "motion";
        }

        return null;
    }

    internal static CameraEventType CreateType(string key) => key switch
    {
                "motion" => new CameraEventType(key, "Motion"),
                "person" => new CameraEventType(key, "Person"),
                "vehicle" => new CameraEventType(key, "Vehicle"),
                "animal" => new CameraEventType(key, "Animal"),
                "face" => new CameraEventType(key, "Face"),
                "tamper" => new CameraEventType(key, "Tamper"),
                "linecrossing" => new CameraEventType(key, "Line crossing"),
                "intrusion" => new CameraEventType(key, "Intrusion"),
                "loitering" => new CameraEventType(key, "Loitering"),
                "sound" => new CameraEventType(key, "Sound"),
        _ => new CameraEventType(key, key),
    };

    private static HttpClient CreateClient(Uri serviceUri) =>
        CameraHttpClientFactory.Create(serviceUri, credentials: null, PullTimeout);
}
