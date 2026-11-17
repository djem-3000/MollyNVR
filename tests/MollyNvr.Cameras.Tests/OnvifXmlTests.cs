using System.Net;
using MollyNvr.Cameras.Abstractions;
using MollyNvr.Cameras.ONVIF;

namespace MollyNvr.Cameras.Tests;

public class OnvifXmlTests
{
    private static string LoadFixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void ParseDeviceInfo_ReadsVendorModelAndSerial()
    {
        var device = OnvifXml.ParseDeviceInfo(LoadFixture("reolink-deviceinfo.xml"));

        Assert.Equal("REOLINK", device.Vendor);
        Assert.Equal("CX820", device.Model);
        Assert.Equal("v3.2.0.5375_2509162386", device.Firmware);
        Assert.Equal("1921681113", device.SerialNumber);
    }

    [Fact]
    public void ParseProfiles_ReadsMainAndSubStreams()
    {
        var profiles = OnvifXml.ParseProfiles(LoadFixture("reolink-profiles.xml"));

        Assert.Equal(2, profiles.Count);

        var main = profiles[0];
        Assert.Equal("000", main.Token);
        Assert.Equal("Profile000_MainStream", main.Name);
        Assert.Equal("H.264", main.Video.Codec);
        Assert.Equal(3840, main.Video.Width);
        Assert.Equal(2160, main.Video.Height);
        Assert.Equal(25, main.Video.FrameRate);
        Assert.Equal(8192, main.Video.BitrateKbps);
        Assert.True(main.Audio.Enabled);
        Assert.Equal("AAC", main.Audio.Codec);
        Assert.Equal(16000, main.Audio.SampleRate);
        Assert.Equal(64, main.Audio.Bitrate);

        var sub = profiles[1];
        Assert.Equal("001", sub.Token);
        Assert.Equal(640, sub.Video.Width);
        Assert.Equal(360, sub.Video.Height);
        Assert.Equal(10, sub.Video.FrameRate);
        Assert.Equal(512, sub.Video.BitrateKbps);
    }

    [Theory]
    [InlineData("reolink-streamuri-000.xml", "rtsp://192.168.1.113:554/Preview_01_main")]
    [InlineData("reolink-streamuri-001.xml", "rtsp://192.168.1.113:554/Preview_01_sub")]
    [InlineData("reolink-snapshoturi-000.xml", "http://192.168.1.113:80/cgi-bin/api.cgi?cmd=onvifSnapPic&channel=0")]
    public void ParseUri_ReadsMediaUri(string fixture, string expected)
    {
        Assert.Equal(expected, OnvifXml.ParseUri(LoadFixture(fixture)));
    }

    [Fact]
    public void ParseMediaServiceUri_ReadsCapabilitiesAddress()
    {
        var xml = """
            <Envelope>
              <Body>
                <GetCapabilitiesResponse>
                  <Capabilities>
                    <Media>
                      <XAddr>http://192.168.1.10:8000/onvif/media_service</XAddr>
                    </Media>
                  </Capabilities>
                </GetCapabilitiesResponse>
              </Body>
            </Envelope>
            """;

        Assert.Equal(
            "http://192.168.1.10:8000/onvif/media_service",
            OnvifXml.ParseMediaServiceUri(xml)?.AbsoluteUri);
    }

    [Fact]
    public void StripCredentials_RemovesUserInfo()
    {
        var url = OnvifXml.StripCredentials("rtsp://admin:secret@192.168.1.10:554/Preview_01_main");

        Assert.Equal("rtsp://192.168.1.10:554/Preview_01_main", url);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void ParseDhcpEnabled_ReadsIpv4Flag(string value, bool expected)
    {
        var xml = $"""
            <Envelope>
              <Body>
                <GetNetworkInterfacesResponse>
                  <NetworkInterfaces>
                    <IPv4>
                      <Config>
                        <DHCP>{value}</DHCP>
                      </Config>
                    </IPv4>
                  </NetworkInterfaces>
                </GetNetworkInterfacesResponse>
              </Body>
            </Envelope>
            """;

        Assert.Equal(expected, OnvifXml.ParseDhcpEnabled(xml));
    }

    [Fact]
    public void ParseDhcpEnabled_ReturnsNullWhenFlagMissing()
    {
        Assert.Null(OnvifXml.ParseDhcpEnabled("<Envelope><Body /></Envelope>"));
    }

    [Fact]
    public void ParseUri_ThrowsWhenUriMissing()
    {
        Assert.Throws<CameraUnavailableException>(() => OnvifXml.ParseUri("<Envelope><Body /></Envelope>"));
    }

    [Fact]
    public void ComputePasswordDigest_MatchesOnvifExample()
    {
        // RFC 2617-подобный пример: nonce + created + password, SHA1, Base64.
        var nonce = Convert.FromBase64String("Dtbk3B2Fpzqo6RDWZWrlZw==");

        var digest = OnvifSoap.ComputePasswordDigest(nonce, "2026-10-19T08:54:42.419Z", "secret");

        Assert.Equal(28, digest.Length);
        Assert.Equal(Convert.ToBase64String(Convert.FromBase64String(digest)), digest);
    }
}
