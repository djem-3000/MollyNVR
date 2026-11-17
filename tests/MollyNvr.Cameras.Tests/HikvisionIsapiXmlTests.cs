using System.Net;
using MollyNvr.Cameras.Abstractions;
using MollyNvr.Cameras.Hikvision;

namespace MollyNvr.Cameras.Tests;

public class HikvisionIsapiXmlTests
{
    private static string LoadFixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static CameraEndpoint Endpoint => new(IPAddress.Parse("192.168.1.115"));

    [Fact]
    public void ParseDeviceInfo_ReadsModelFirmwareAndSerial()
    {
        var device = HikvisionIsapiXml.ParseDeviceInfo(LoadFixture("hikvision-deviceinfo.xml"));

        Assert.Equal("Hikvision", device.Vendor);
        Assert.Equal("DS-KV9503-WBE1", device.Model);
        Assert.Equal("V2.3.13", device.Firmware);
        Assert.Equal("DS-KV9503-WBE10020240618ENFG0903384", device.SerialNumber);
        Assert.Equal("OUTDOOR STATION", device.DeviceName);
    }

    [Fact]
    public void ParseProfiles_SelectsMainAndAllSubstreams()
    {
        var profiles = HikvisionIsapiXml.ParseProfiles(LoadFixture("hikvision-channels.xml"), Endpoint);

        Assert.Equal(3, profiles.Count);

        var main = Assert.Single(profiles, profile => profile.Kind == CameraStreamProfileKind.Main);
        Assert.Equal("101", main.Id);
        Assert.Equal(1920, main.Video.Width);
        Assert.Equal(1080, main.Video.Height);
        Assert.Equal("H.264", main.Video.Codec);
        Assert.Equal(20, main.Video.FrameRate);
        Assert.Equal(2048, main.Video.BitrateKbps);
        Assert.True(main.Audio.Enabled);
        Assert.Equal("G.711ulaw", main.Audio.Codec);
        Assert.Equal(8000, main.Audio.SampleRate);
        Assert.Equal("rtsp://192.168.1.115:554/Streaming/Channels/101", main.RtspUrl);

        var substreams = profiles.Where(profile => profile.Kind == CameraStreamProfileKind.Sub).ToList();
        Assert.Equal(["103", "102"], substreams.Select(static profile => profile.Id));
        Assert.Equal(1280, substreams[0].Video.Width);
        Assert.Equal(720, substreams[0].Video.Height);
        Assert.Equal("H.265", substreams[0].Video.Codec);
        Assert.Equal(640, substreams[1].Video.Width);
        Assert.Equal(480, substreams[1].Video.Height);
    }

    [Theory]
    [InlineData("dynamic", true)]
    [InlineData("dhcp", true)]
    [InlineData("static", false)]
    public void ParseDhcpEnabled_ReadsAddressingType(string addressingType, bool expected)
    {
        var xml = $"""
            <NetworkInterfaceList>
              <NetworkInterface>
                <IPAddress>
                  <addressingType>{addressingType}</addressingType>
                </IPAddress>
              </NetworkInterface>
            </NetworkInterfaceList>
            """;

        Assert.Equal(expected, HikvisionIsapiXml.ParseDhcpEnabled(xml));
    }

    [Fact]
    public void ParseDhcpEnabled_ReturnsNullWhenAddressingTypeMissing()
    {
        Assert.Null(HikvisionIsapiXml.ParseDhcpEnabled("<NetworkInterfaceList />"));
    }

    [Fact]
    public void ParseProfiles_UsesCustomRtspPort()
    {
        var endpoint = new CameraEndpoint(IPAddress.Parse("10.0.0.5"), RtspPort: 10554);

        var profiles = HikvisionIsapiXml.ParseProfiles(LoadFixture("hikvision-channels.xml"), endpoint);

        Assert.All(profiles, profile => Assert.StartsWith("rtsp://10.0.0.5:10554/", profile.RtspUrl, StringComparison.Ordinal));
    }

    [Fact]
    public void ReadErrorStatus_ReturnsStatusCodeAndMessage()
    {
        const string body = """
            <?xml version="1.0" encoding="UTF-8"?>
            <ResponseStatus version="2.0" xmlns="http://www.isapi.org/ver20/XMLSchema">
            <requestURL>/ISAPI/Streaming/channels/101</requestURL>
            <statusCode>4</statusCode>
            <statusString>Invalid Operation</statusString>
            </ResponseStatus>
            """;

        Assert.Equal("4 Invalid Operation", HikvisionIsapiXml.ReadErrorStatus(body));
    }

    [Fact]
    public void ParseDeviceInfo_ThrowsOnUnexpectedRoot()
    {
        Assert.Throws<CameraUnavailableException>(() =>
            HikvisionIsapiXml.ParseDeviceInfo("<StreamingChannelList />"));
    }
}
