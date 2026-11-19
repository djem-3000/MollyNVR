using System.Globalization;
using System.Xml.Linq;
using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Cameras.Hikvision;

/// <summary>ISAPI (Hikvision) XML parsing and codec-change document building.</summary>
internal static class HikvisionIsapiXml
{
        /// <summary>Parses the <c>/ISAPI/System/deviceInfo</c> response.</summary>
    public static CameraDeviceInfo ParseDeviceInfo(string xml)
    {
        var root = LoadRoot(xml, "DeviceInfo");

        return new CameraDeviceInfo(
            Vendor: "Hikvision",
            Model: Value(root, "model") ?? "Hikvision",
            Firmware: Value(root, "firmwareVersion") ?? string.Empty,
            SerialNumber: Value(root, "serialNumber"),
            DeviceName: Value(root, "deviceName"));
    }

        /// <summary>Parses the DHCP state from the <c>/ISAPI/System/Network/interfaces</c> response.</summary>
    public static bool? ParseDhcpEnabled(string xml)
    {
        var root = LoadRoot(xml, "NetworkInterfaceList");
        var addressingType = root.Descendants()
            .FirstOrDefault(static element => element.Name.LocalName == "addressingType")
            ?.Value
            .Trim();

        if (!string.IsNullOrWhiteSpace(addressingType))
        {
            return addressingType.ToLowerInvariant() switch
            {
                "dynamic" or "dhcp" => true,
                "static" => false,
                _ => null,
            };
        }

        var dhcp = root.Descendants()
            .FirstOrDefault(static element => element.Name.LocalName == "DHCP")
            ?.Value
            .Trim();

        return dhcp is not null && bool.TryParse(dhcp, out var enabled) ? enabled : null;
    }

        /// <summary>Parses the <c>/ISAPI/Streaming/channels</c> response into main/sub profiles.</summary>
    public static IReadOnlyList<CameraProfile> ParseProfiles(string xml, CameraEndpoint endpoint)
    {
        var root = LoadRoot(xml, "StreamingChannelList");
        var channels = root.Descendants()
            .Where(static element => element.Name.LocalName == "StreamingChannel")
            .Select(element => ParseChannel(element, endpoint))
            .Where(static channel => channel is not null)
            .Select(static channel => channel!)
            .ToList();

        return SelectProfiles(channels);
    }

        /// <summary>Extracts a human-readable ISAPI error description.</summary>
    public static string? ReadErrorStatus(string body)
    {
        if (string.IsNullOrWhiteSpace(body) || !body.Contains("ResponseStatus", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var root = XDocument.Parse(body).Root;
            if (root is null)
            {
                return null;
            }

            var code = Value(root, "statusCode");
            var message = Value(root, "statusString");
            return string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(message)
                ? null
                : $"{code} {message}".Trim();
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private static CameraProfile? ParseChannel(XElement channel, CameraEndpoint endpoint)
    {
        var id = Value(channel, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var video = channel.Elements().FirstOrDefault(static element => element.Name.LocalName == "Video");
        if (video is null || !IsEnabled(video))
        {
            return null;
        }

        var audio = channel.Elements().FirstOrDefault(static element => element.Name.LocalName == "Audio");
        var audioCodec = audio is null ? null : Value(audio, "audioCompressionType");
        var audioEnabled = audio is not null && IsEnabled(audio);

        return new CameraProfile(
            Kind: CameraStreamProfileKind.Sub,
            Id: id,
            Name: Value(channel, "channelName"),
            Video: new CameraVideoSettings(
                Codec: VideoCodecNames.Normalize(Value(video, "videoCodecType")),
                Width: IntValue(video, "videoResolutionWidth"),
                Height: IntValue(video, "videoResolutionHeight"),
                FrameRate: IntValue(video, "maxFrameRate") / 100d,
                BitrateKbps: IntValue(video, "vbrUpperCap") is var cap && cap > 0
                    ? cap
                    : IntValue(video, "constantBitRate")),
            Audio: new CameraAudioSettings(
                Enabled: audioEnabled,
                Codec: audioEnabled ? AudioCodecNames.Normalize(audioCodec) : null,
                SampleRate: audioEnabled ? AudioCodecNames.KnownSampleRate(audioCodec) : null,
                Bitrate: audioEnabled && audio is not null ? IntValue(audio, "audioBitrate") : null),
            RtspUrl: endpoint.BuildRtspUri($"/Streaming/Channels/{id}").AbsoluteUri);
    }

    private static IReadOnlyList<CameraProfile> SelectProfiles(IReadOnlyList<CameraProfile> channels)
    {
        if (channels.Count == 0)
        {
            return [];
        }

        var ordered = channels
            .OrderByDescending(static channel => channel.Video.Width * channel.Video.Height)
            .ToList();

        var profiles = new List<CameraProfile>
        {
            ordered[0] with { Kind = CameraStreamProfileKind.Main },
        };
        profiles.AddRange(ordered.Skip(1).Select(static profile => profile with { Kind = CameraStreamProfileKind.Sub }));
        return profiles;
    }

    private static XElement LoadRoot(string xml, string expectedRoot)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
                        throw new CameraUnavailableException("ISAPI returned an empty response.");
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException exception)
        {
                        throw new CameraUnavailableException("ISAPI returned invalid XML.", exception);
        }

        var root = document.Root
                        ?? throw new CameraUnavailableException("ISAPI returned XML without a root element.");

        if (!string.Equals(root.Name.LocalName, expectedRoot, StringComparison.Ordinal))
        {
                        throw new CameraUnavailableException($"ISAPI returned unexpected element '{root.Name.LocalName}' instead of '{expectedRoot}'.");
        }

        return root;
    }

    private static bool IsEnabled(XElement parent) =>
        !string.Equals(Value(parent, "enabled"), "false", StringComparison.OrdinalIgnoreCase);

    private static string? Value(XElement parent, string localName) =>
        parent.Elements()
            .FirstOrDefault(element => element.Name.LocalName == localName)
            ?.Value
            .Trim();

    private static int IntValue(XElement parent, string localName) =>
        int.TryParse(Value(parent, localName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
