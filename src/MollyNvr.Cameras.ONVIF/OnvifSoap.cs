using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Cameras.ONVIF;

/// <summary>Сборка SOAP-запросов ONVIF и разбор ответов (WS-Security UsernameToken).</summary>
internal static class OnvifSoap
{
    private static readonly XNamespace Soap = "http://www.w3.org/2003/05/soap-envelope";
    private static readonly XNamespace Wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    private static readonly XNamespace Wsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
    private static readonly XNamespace Media = "http://www.onvif.org/ver10/media/wsdl";
    private static readonly XNamespace Schema = "http://www.onvif.org/ver10/schema";
    private static readonly XNamespace Device = "http://www.onvif.org/ver10/device/wsdl";

    /// <summary>Действие SOAP для операций ONVIF.</summary>
    public static class Actions
    {
        public const string GetDeviceInformation = "http://www.onvif.org/ver10/device/wsdl/GetDeviceInformation";
        public const string GetCapabilities = "http://www.onvif.org/ver10/device/wsdl/GetCapabilities";
        public const string GetNetworkInterfaces = "http://www.onvif.org/ver10/device/wsdl/GetNetworkInterfaces";
        public const string GetProfiles = "http://www.onvif.org/ver10/media/wsdl/GetProfiles";
        public const string GetStreamUri = "http://www.onvif.org/ver10/media/wsdl/GetStreamUri";
        public const string GetSnapshotUri = "http://www.onvif.org/ver10/media/wsdl/GetSnapshotUri";
    }

    /// <summary>Собирает SOAP-конверт с WS-Security UsernameToken (PasswordDigest).</summary>
    public static string BuildEnvelope(CameraCredentials? credentials, XElement body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var header = credentials is null
            ? new XElement(Soap + "Header")
            : new XElement(Soap + "Header", BuildSecurityHeader(credentials));

        var envelope = new XElement(
            Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "s", Soap.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "wsse", Wsse.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "wsu", Wsu.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "tds", Device.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "trt", Media.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "tt", Schema.NamespaceName),
            header,
            new XElement(Soap + "Body", body));

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), envelope).ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>Формирует тело запроса <c>GetStreamUri</c>.</summary>
    public static XElement CreateGetStreamUri(string profileToken) =>
        new(
            Media + "GetStreamUri",
            new XElement(
                Media + "StreamSetup",
                new XElement(Schema + "Stream", "RTP-Unicast"),
                new XElement(Schema + "Transport", new XElement(Schema + "Protocol", "RTSP"))),
            new XElement(Media + "ProfileToken", profileToken));

    /// <summary>Формирует тело запроса <c>GetSnapshotUri</c>.</summary>
    public static XElement CreateGetSnapshotUri(string profileToken) =>
        new(Media + "GetSnapshotUri", new XElement(Media + "ProfileToken", profileToken));

    /// <summary>Формирует тело запроса <c>GetProfiles</c>.</summary>
    public static XElement CreateGetProfiles() => new(Media + "GetProfiles");

    /// <summary>Формирует тело запроса <c>GetDeviceInformation</c>.</summary>
    public static XElement CreateGetDeviceInformation() => new(Device + "GetDeviceInformation");

    /// <summary>Формирует тело запроса <c>GetCapabilities</c>.</summary>
    public static XElement CreateGetCapabilities() => CreateGetCapabilities("Media");

    /// <summary>Формирует тело запроса <c>GetCapabilities</c> для указанной категории.</summary>
    public static XElement CreateGetCapabilities(string category) =>
        new(
            Device + "GetCapabilities",
            new XElement(Device + "Category", category));

    /// <summary>Формирует тело запроса <c>GetNetworkInterfaces</c>.</summary>
    public static XElement CreateGetNetworkInterfaces() => new(Device + "GetNetworkInterfaces");

    /// <summary>Проверяет, что ответ является SOAP-конвертом ONVIF.</summary>
    public static bool IsSoapEnvelope(string xml) =>
        !string.IsNullOrWhiteSpace(xml) &&
        xml.Contains("Envelope", StringComparison.Ordinal) &&
        xml.Contains("http://www.w3.org/2003/05/soap-envelope", StringComparison.Ordinal);

    /// <summary>Извлекает текст ошибки SOAP, если ответ содержит Fault.</summary>
    public static string? ReadFault(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }

        var fault = document.Descendants().FirstOrDefault(static element => element.Name.LocalName == "Fault");
        if (fault is null)
        {
            return null;
        }

        var reason = fault.Descendants()
            .FirstOrDefault(static element => element.Name.LocalName == "Text")
            ?.Value
            .Trim();

        var subcode = fault.Descendants()
            .FirstOrDefault(static element => element.Name.LocalName == "Subcode")
            ?.Descendants()
            .FirstOrDefault(static element => element.Name.LocalName == "Value")
            ?.Value
            .Trim();

        return string.IsNullOrWhiteSpace(reason)
            ? subcode ?? "ONVIF вернул ошибку без описания."
            : string.IsNullOrWhiteSpace(subcode) ? reason : $"{subcode}: {reason}";
    }

    private static XElement BuildSecurityHeader(CameraCredentials credentials)
    {
        var nonce = RandomNumberGenerator.GetBytes(16);
        var created = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
        var digest = ComputePasswordDigest(nonce, created, credentials.Password);

        return new XElement(
            Wsse + "Security",
            new XElement(
                Wsse + "UsernameToken",
                new XElement(Wsse + "Username", credentials.UserName),
                new XElement(
                    Wsse + "Password",
                    new XAttribute("Type", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest"),
                    digest),
                new XElement(
                    Wsse + "Nonce",
                    new XAttribute("EncodingType", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary"),
                    Convert.ToBase64String(nonce)),
                new XElement(Wsu + "Created", created)));
    }

    /// <summary>PasswordDigest = Base64(SHA1(nonce + created + password)).</summary>
    public static string ComputePasswordDigest(byte[] nonce, string created, string password)
    {
        ArgumentNullException.ThrowIfNull(nonce);
        ArgumentNullException.ThrowIfNull(created);
        ArgumentNullException.ThrowIfNull(password);

        var createdBytes = Encoding.UTF8.GetBytes(created);
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var buffer = new byte[nonce.Length + createdBytes.Length + passwordBytes.Length];

        nonce.CopyTo(buffer, 0);
        createdBytes.CopyTo(buffer, nonce.Length);
        passwordBytes.CopyTo(buffer, nonce.Length + createdBytes.Length);

        return Convert.ToBase64String(SHA1.HashData(buffer));
    }
}
