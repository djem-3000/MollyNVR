using System.Globalization;
using System.Xml.Linq;
using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Cameras.ONVIF;

/// <summary>Сырые данные профиля ONVIF до получения RTSP-URL.</summary>
internal sealed record OnvifProfile(
    string Token,
    string? Name,
    CameraVideoSettings Video,
    CameraAudioSettings Audio);

/// <summary>Разбор ответов ONVIF (пространства имён игнорируются, сравнение по локальному имени).</summary>
internal static class OnvifXml
{
    /// <summary>Разбирает ответ <c>GetDeviceInformation</c>.</summary>
    public static CameraDeviceInfo ParseDeviceInfo(string xml)
    {
        var response = FindResponse(xml, "GetDeviceInformationResponse");

        return new CameraDeviceInfo(
            Vendor: Value(response, "Manufacturer") ?? "ONVIF",
            Model: Value(response, "Model") ?? "ONVIF",
            Firmware: Value(response, "FirmwareVersion") ?? string.Empty,
            SerialNumber: Value(response, "SerialNumber"),
            DeviceName: null);
    }

    /// <summary>Разбирает ответ <c>GetProfiles</c>. Профили без видеокодировщика пропускаются.</summary>
    public static IReadOnlyList<OnvifProfile> ParseProfiles(string xml)
    {
        var document = Load(xml);

        return document.Descendants()
            .Where(static element => element.Name.LocalName == "Profiles")
            .Select(TryParseProfile)
            .Where(static profile => profile is not null)
            .Cast<OnvifProfile>()
            .ToList();
    }

    /// <summary>Извлекает адрес Media-сервиса из ответа <c>GetCapabilities</c>.</summary>
    public static Uri? ParseMediaServiceUri(string xml)
    {
        var document = Load(xml);
        var media = document.Descendants()
            .FirstOrDefault(static element => element.Name.LocalName == "Media");
        var address = media?.Descendants()
            .FirstOrDefault(static element => element.Name.LocalName == "XAddr")
            ?.Value
            .Trim();

        return Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri : null;
    }

    /// <summary>Извлекает RTSP-URL из ответа <c>GetStreamUri</c> или <c>GetSnapshotUri</c>.</summary>
    public static string ParseUri(string xml)
    {
        var document = Load(xml);
        var uri = document.Descendants()
            .FirstOrDefault(static element => element.Name.LocalName == "Uri")
            ?.Value
            .Trim();

        return string.IsNullOrWhiteSpace(uri)
            ? throw new CameraUnavailableException("ONVIF не вернул адрес потока.")
            : uri;
    }

    /// <summary>Разбирает состояние DHCP из ответа <c>GetNetworkInterfaces</c>.</summary>
    public static bool? ParseDhcpEnabled(string xml)
    {
        var document = Load(xml);
        var dhcp = document.Descendants()
            .FirstOrDefault(static element =>
                element.Name.LocalName == "DHCP" &&
                element.Ancestors().Any(static ancestor => ancestor.Name.LocalName == "IPv4"));

        return dhcp is not null && bool.TryParse(dhcp.Value.Trim(), out var enabled)
            ? enabled
            : null;
    }

    /// <summary>Приводит RTSP-URL к виду без учётных данных.</summary>
    public static string StripCredentials(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.UserInfo))
        {
            return url;
        }

        return new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty }.Uri.AbsoluteUri;
    }

    private static OnvifProfile? TryParseProfile(XElement element)
    {
        var token = (string?)element.Attribute("token");
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var video = element.Elements()
            .FirstOrDefault(static child => child.Name.LocalName == "VideoEncoderConfiguration");
        if (video is null)
        {
            return null;
        }

        var resolution = video.Elements().FirstOrDefault(static child => child.Name.LocalName == "Resolution");
        var rateControl = video.Elements().FirstOrDefault(static child => child.Name.LocalName == "RateControl");
        var audio = element.Elements().FirstOrDefault(static child => child.Name.LocalName == "AudioEncoderConfiguration");

        var audioCodec = audio is null ? null : AudioCodecNames.Normalize(Value(audio, "Encoding"));
        var sampleRateKhz = IntValue(audio, "SampleRate");

        return new OnvifProfile(
            Token: token,
            Name: Value(element, "Name"),
            Video: new CameraVideoSettings(
                Codec: VideoCodecNames.Normalize(Value(video, "Encoding")),
                Width: IntValue(resolution, "Width"),
                Height: IntValue(resolution, "Height"),
                FrameRate: DoubleValue(rateControl, "FrameRateLimit"),
                BitrateKbps: IntValue(rateControl, "BitrateLimit")),
            Audio: new CameraAudioSettings(
                Enabled: audio is not null,
                Codec: audioCodec,
                SampleRate: sampleRateKhz > 0 ? sampleRateKhz * 1000 : AudioCodecNames.KnownSampleRate(audioCodec),
                Bitrate: IntValue(audio, "Bitrate")));
    }

    private static XElement FindResponse(string xml, string responseName)
    {
        var document = Load(xml);

        return document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == responseName)
            ?? throw new CameraUnavailableException($"ONVIF вернул ответ без элемента «{responseName}».");
    }

    private static XDocument Load(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            throw new CameraUnavailableException("ONVIF вернул пустой ответ.");
        }

        try
        {
            return XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException exception)
        {
            throw new CameraUnavailableException("ONVIF вернул некорректный XML.", exception);
        }
    }

    private static string? Value(XElement? parent, string localName) =>
        parent?.Elements()
            .FirstOrDefault(element => element.Name.LocalName == localName)
            ?.Value
            .Trim();

    private static int IntValue(XElement? parent, string localName) =>
        int.TryParse(Value(parent, localName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    private static double DoubleValue(XElement? parent, string localName) =>
        double.TryParse(Value(parent, localName), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
}
