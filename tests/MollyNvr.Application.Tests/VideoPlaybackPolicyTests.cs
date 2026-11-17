using MollyNvr.Application.Cameras;

namespace MollyNvr.Application.Tests;

public class VideoPlaybackPolicyTests
{
    [Theory]
    [InlineData("H.265", true)]
    [InlineData("H265", true)]
    [InlineData("HEVC", true)]
    [InlineData("H.264", false)]
    [InlineData("H264", false)]
    [InlineData("AVC", false)]
    [InlineData(null, false)]
    public void NeedsTranscode_ReturnsExpectedValue(string? codec, bool expected)
    {
        Assert.Equal(expected, VideoPlaybackPolicy.NeedsTranscode(codec));
    }

    [Fact]
    public void TargetCodecFor_H265_ReturnsH264()
    {
        Assert.Equal("H264", VideoPlaybackPolicy.TargetCodecFor("H.265"));
        Assert.Null(VideoPlaybackPolicy.TargetCodecFor("H.264"));
    }

    [Fact]
    public void ToApiValue_ReturnsExpectedStrings()
    {
        Assert.Equal("transcode-available", VideoPlaybackPolicy.ToApiValue("H.265"));
        Assert.Equal("passthrough", VideoPlaybackPolicy.ToApiValue("H.264"));
    }
}
