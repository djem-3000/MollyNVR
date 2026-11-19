using System.Net;

namespace MollyNvr.Cameras.Abstractions;

/// <summary>Camera network address: host and service ports.</summary>
public sealed record CameraEndpoint(IPAddress Address, int RtspPort = 554, int HttpPort = 80, int OnvifPort = 8000)
{
    public string Host => Address.ToString();

    public Uri BuildRtspUri(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalized = path.StartsWith('/') ? path : "/" + path;
        return new UriBuilder("rtsp", Host, RtspPort, normalized).Uri;
    }

    public Uri BuildHttpUri(string path, int? port = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalized = path.StartsWith('/') ? path : "/" + path;
        return new UriBuilder(Uri.UriSchemeHttp, Host, port ?? HttpPort, normalized).Uri;
    }

        /// <summary>URL of the device ONVIF service (SOAP requests).</summary>
    public Uri OnvifServiceUri => BuildHttpUri("/onvif/device_service", OnvifPort);
}
