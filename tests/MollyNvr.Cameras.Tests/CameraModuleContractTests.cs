using System.Net;
using MollyNvr.Cameras.Abstractions;
using MollyNvr.Cameras.Hikvision;
using MollyNvr.Cameras.ONVIF;

namespace MollyNvr.Cameras.Tests;

public class CameraModuleContractTests
{
    [Fact]
    public void Hikvision_ExposesKeyAndName()
    {
        var module = new HikvisionCameraModule();

        Assert.Equal("hikvision", module.Key);
        Assert.Equal("Hikvision", module.DisplayName);
    }

    [Fact]
    public void Onvif_ExposesKeyAndName()
    {
        var module = new OnvifCameraModule();

        Assert.Equal("onvif", module.Key);
        Assert.Equal("ONVIF", module.DisplayName);
    }

    [Fact]
    public void Endpoint_BuildsRtspAndHttpUris()
    {
        var endpoint = new CameraEndpoint(IPAddress.Parse("192.168.1.10"), RtspPort: 554, HttpPort: 80, OnvifPort: 8000);

        Assert.Equal("rtsp://192.168.1.10:554/Streaming/Channels/101", endpoint.BuildRtspUri("/Streaming/Channels/101").AbsoluteUri);
        Assert.Equal("http://192.168.1.10/ISAPI/System/deviceInfo", endpoint.BuildHttpUri("/ISAPI/System/deviceInfo").AbsoluteUri);
        Assert.Equal("http://192.168.1.10:8000/onvif/device_service", endpoint.OnvifServiceUri.AbsoluteUri);
    }

    [Fact]
    public void WithCredentials_AddsUserInfoAndEscapesPassword()
    {
        var uri = new Uri("rtsp://192.168.1.10:554/Preview_01_main");
        var url = RtspUriFactory.WithCredentials(uri, new CameraCredentials("nvr", "p@ss:word"));

        Assert.Equal("rtsp://nvr:p%40ss%3Aword@192.168.1.10:554/Preview_01_main", url);
    }

    [Theory]
    [InlineData("H264", "H.264")]
    [InlineData("H.265", "H.265")]
    [InlineData("hevc", "H.265")]
    [InlineData("avc1", "H.264")]
    [InlineData("mjpg", "MJPEG")]
    public void VideoCodecNames_NormalizesVendorNames(string input, string expected)
    {
        Assert.Equal(expected, VideoCodecNames.Normalize(input));
    }

    [Theory]
    [InlineData("PCMU", "G.711ulaw")]
    [InlineData("PCMA", "G.711alaw")]
    [InlineData("MPEG4-GENERIC", "AAC")]
    [InlineData("G.711ulaw", "G.711ulaw")]
    public void AudioCodecNames_NormalizesVendorNames(string input, string expected)
    {
        Assert.Equal(expected, AudioCodecNames.Normalize(input));
    }

    [Fact]
    public void Credentials_ToStringMasksPassword()
    {
        var credentials = new CameraCredentials("admin", "secret");

        Assert.Equal("admin:***", credentials.ToString());
    }
}
