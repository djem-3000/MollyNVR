using MollyNvr.Application.Cameras;

namespace MollyNvr.Application.Tests;

public class AudioPlaybackPolicyTests
{
    [Theory]
    [InlineData("AAC", AudioPlaybackMode.Transcoded)]
    [InlineData("MPEG4-GENERIC", AudioPlaybackMode.Transcoded)]
    [InlineData("G.722.1", AudioPlaybackMode.Unsupported)]
    [InlineData("G7221", AudioPlaybackMode.Unsupported)]
    [InlineData("G.711ulaw", AudioPlaybackMode.Passthrough)]
    [InlineData("PCMU", AudioPlaybackMode.Passthrough)]
    [InlineData("PCMA", AudioPlaybackMode.Passthrough)]
    [InlineData("OPUS", AudioPlaybackMode.Passthrough)]
    public void Classify_ReturnsExpectedMode(string codec, AudioPlaybackMode expected)
    {
        Assert.Equal(expected, AudioPlaybackPolicy.Classify(codec));
    }

    [Fact]
    public void TargetCodecFor_Transcoded_ReturnsOpus()
    {
        Assert.Equal("Opus", AudioPlaybackPolicy.TargetCodecFor(AudioPlaybackMode.Transcoded));
        Assert.Null(AudioPlaybackPolicy.TargetCodecFor(AudioPlaybackMode.Passthrough));
        Assert.Null(AudioPlaybackPolicy.TargetCodecFor(AudioPlaybackMode.Unsupported));
    }

    [Fact]
    public void ToApiValue_ReturnsLowercaseNames()
    {
        Assert.Equal("transcoded", AudioPlaybackPolicy.ToApiValue(AudioPlaybackMode.Transcoded));
        Assert.Equal("unsupported", AudioPlaybackPolicy.ToApiValue(AudioPlaybackMode.Unsupported));
        Assert.Equal("passthrough", AudioPlaybackPolicy.ToApiValue(AudioPlaybackMode.Passthrough));
        Assert.Equal("disabled", AudioPlaybackPolicy.ToApiValue(AudioPlaybackMode.Disabled));
    }
}
