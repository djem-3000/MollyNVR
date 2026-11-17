using System.Net;
using MollyNvr.Application.Discovery;

namespace MollyNvr.Application.Tests;

public class IpRangeTests
{
    [Fact]
    public void Parse_Cidr_ReturnsAllAddresses()
    {
        var hosts = IpRange.Parse("192.168.1.0/30").Hosts();

        Assert.Equal(
            [IPAddress.Parse("192.168.1.0"), IPAddress.Parse("192.168.1.1"),
             IPAddress.Parse("192.168.1.2"), IPAddress.Parse("192.168.1.3")],
            hosts);
    }

    [Fact]
    public void Parse_LastOctetRange_ReturnsInclusiveRange()
    {
        var hosts = IpRange.Parse("192.168.1.10-12").Hosts();

        Assert.Equal(
            [IPAddress.Parse("192.168.1.10"), IPAddress.Parse("192.168.1.11"), IPAddress.Parse("192.168.1.12")],
            hosts);
    }

    [Fact]
    public void Parse_FullRange_ReturnsInclusiveRange()
    {
        var hosts = IpRange.Parse("10.0.0.254-10.0.1.1").Hosts();

        Assert.Equal(4, hosts.Count);
        Assert.Equal(IPAddress.Parse("10.0.0.254"), hosts[0]);
        Assert.Equal(IPAddress.Parse("10.0.1.1"), hosts[^1]);
    }

    [Fact]
    public void Parse_SingleAddress_ReturnsOneHost()
    {
        var hosts = IpRange.Parse(" 192.168.1.113 ").Hosts();

        Assert.Equal([IPAddress.Parse("192.168.1.113")], hosts);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("192.168.1.300")]
    [InlineData("192.168.1.0/33")]
    [InlineData("192.168.1.10-5")]
    [InlineData("::1")]
    [InlineData("192.168.1.1/24/8")]
    public void Parse_InvalidValue_Throws(string value)
    {
        Assert.Throws<ArgumentException>(() => IpRange.Parse(value));
    }

    [Fact]
    public void Parse_TooLargeRange_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => IpRange.Parse("10.0.0.0/16"));

        Assert.Contains("1024", exception.Message, StringComparison.Ordinal);
    }
}
