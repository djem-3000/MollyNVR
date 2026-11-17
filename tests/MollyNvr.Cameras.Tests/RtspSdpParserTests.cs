using MollyNvr.Infrastructure.Media;

namespace MollyNvr.Cameras.Tests;

public class RtspSdpParserTests
{
    [Fact]
    public void Parse_ReadsH264AndPcmuFromHikvisionSdp()
    {
        var sdp = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "rtsp-sdp-hikvision.txt"));
        var media = RtspSdpParser.Parse(sdp);

        Assert.NotNull(media);
        Assert.Equal("H.264", media.VideoCodec);
        Assert.Equal("G.711ulaw", media.AudioCodec);
        Assert.Equal(8000, media.AudioSampleRate);
    }

    [Fact]
    public void Parse_ReadsHevcAndAac()
    {
        const string sdp = """
            v=0
            o=- 0 0 IN IP4 192.168.1.113
            s=Media Presentation
            m=video 0 RTP/AVP 96
            a=rtpmap:96 H265/90000
            m=audio 0 RTP/AVP 97
            a=rtpmap:97 MPEG4-GENERIC/16000/1
            """;

        var media = RtspSdpParser.Parse(sdp);

        Assert.NotNull(media);
        Assert.Equal("H.265", media.VideoCodec);
        Assert.Equal("AAC", media.AudioCodec);
        Assert.Equal(16000, media.AudioSampleRate);
    }

    [Fact]
    public void Parse_ReturnsNullWithoutRtpmap()
    {
        const string sdp = """
            v=0
            m=video 0 RTP/AVP 96
            """;

        Assert.Null(RtspSdpParser.Parse(sdp));
    }

    [Fact]
    public void Parse_HandlesVideoOnlyStream()
    {
        const string sdp = """
            v=0
            m=video 0 RTP/AVP 96
            a=rtpmap:96 H264/90000
            """;

        var media = RtspSdpParser.Parse(sdp);

        Assert.NotNull(media);
        Assert.Equal("H.264", media.VideoCodec);
        Assert.Null(media.AudioCodec);
    }
}
