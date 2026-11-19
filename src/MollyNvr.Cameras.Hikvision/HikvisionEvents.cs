using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using MollyNvr.Cameras.Abstractions;
using MollyNvr.Cameras.Abstractions.Http;

namespace MollyNvr.Cameras.Hikvision;

/// <summary>Hikvision events: ISAPI alertStream (multipart) with VMD and detections.</summary>
internal static class HikvisionEvents
{
    private const string AlertStreamPath = "/ISAPI/Event/notification/alertStream";
    private const int MaxBuffer = 512 * 1024;

    private static readonly TimeSpan StreamTimeout = TimeSpan.FromHours(12);

    private static readonly IReadOnlyList<CameraEventType> SupportedTypes =
    [
                new("motion", "Motion"),
                new("linecrossing", "Line crossing"),
                new("intrusion", "Intrusion"),
                new("regionentrance", "Region entrance"),
                new("regionexiting", "Region exit"),
                new("loitering", "Loitering"),
                new("face", "Face"),
                new("person", "Person"),
                new("vehicle", "Vehicle"),
                new("tamper", "Tamper"),
    ];

    public static Task<IReadOnlyList<CameraEventType>> GetEventTypesAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        CancellationToken cancellationToken) =>
        Task.FromResult(SupportedTypes);

    public static async IAsyncEnumerable<CameraEvent> WatchAsync(
        CameraEndpoint endpoint,
        CameraCredentials credentials,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var client = CameraHttpClientFactory.Create(
            endpoint.BuildHttpUri("/"),
            credentials,
            StreamTimeout);

        using var request = new HttpRequestMessage(HttpMethod.Get, AlertStreamPath);
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
                        throw new CameraAuthenticationException("Hikvision rejected the credentials when subscribing to events.");
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var buffer = new StringBuilder();
        var chunk = new char[8192];

        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await reader.ReadAsync(chunk.AsMemory(), cancellationToken);

            if (read == 0)
            {
                yield break;
            }

            buffer.Append(chunk, 0, read);

            while (TryExtract(buffer, out var xml))
            {
                var cameraEvent = Parse(xml);

                if (cameraEvent is not null)
                {
                    yield return cameraEvent;
                }
            }

            if (buffer.Length > MaxBuffer)
            {
                buffer.Clear();
            }
        }
    }

    private static bool TryExtract(StringBuilder buffer, out string xml)
    {
        xml = string.Empty;

        var text = buffer.ToString();
        var start = text.IndexOf("<EventNotification", StringComparison.Ordinal);

        if (start < 0)
        {
                        // Garbage before the first event (multipart headers) can be discarded.
            if (text.Length > 4096)
            {
                buffer.Clear();
            }

            return false;
        }

        const string endTag = "</EventNotification>";
        var end = text.IndexOf(endTag, start, StringComparison.Ordinal);

        if (end < 0)
        {
            if (start > 0)
            {
                buffer.Remove(0, start);
            }

            return false;
        }

        xml = text[start..(end + endTag.Length)];
        buffer.Remove(0, end + endTag.Length);
        return true;
    }

    private static CameraEvent? Parse(string xml)
    {
        XDocument document;

        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }

        var eventType = document.Descendants()
            .FirstOrDefault(static element => element.Name.LocalName == "eventType")?
            .Value;

        var key = ToKey(eventType);

        if (key is null)
        {
            return null;
        }

        var state = document.Descendants()
            .FirstOrDefault(static element => element.Name.LocalName == "eventState")?
            .Value;

        var active = !string.Equals(state, "inactive", StringComparison.OrdinalIgnoreCase);

        return new CameraEvent(key, DateTimeOffset.Now, active);
    }

    internal static string? ToKey(string? eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return null;
        }

        return eventType.Trim().ToLowerInvariant() switch
        {
            "vmd" => "motion",
            "linedetection" => "linecrossing",
            "fielddetection" => "intrusion",
            "regionentrance" => "regionentrance",
            "regionexiting" => "regionexiting",
            "loitering" => "loitering",
            "facedetection" => "face",
            "peopledetection" => "person",
            "humandetection" => "person",
            "vehicledetection" => "vehicle",
            "tamper" => "tamper",
            "videotampering" => "tamper",
            _ => null,
        };
    }
}
