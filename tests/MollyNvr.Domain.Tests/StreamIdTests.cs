using MollyNvr.Domain.Streams;

namespace MollyNvr.Domain.Tests;

public class StreamIdTests
{
    [Theory]
    [InlineData("cam1")]
    [InlineData("front-door")]
    [InlineData("camera_01")]
    [InlineData("A1_b-2")]
    public void Create_AcceptsValidValues(string value)
    {
        var id = StreamId.Create(value);

        Assert.Equal(value, id.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("cam 1")]
    [InlineData("камера")]
    [InlineData("cam/1")]
    [InlineData("cam1@home")]
    [InlineData("cam1?x=1")]
    public void Create_RejectsInvalidValues(string? value)
    {
        Assert.Throws<ArgumentException>(() => StreamId.Create(value));
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        var id = StreamId.Create("  cam1  ");

        Assert.Equal("cam1", id.Value);
    }

    [Fact]
    public void Create_RejectsTooLongValue()
    {
        var value = new string('a', StreamId.MaxLength + 1);

        Assert.Throws<ArgumentException>(() => StreamId.Create(value));
    }
}
