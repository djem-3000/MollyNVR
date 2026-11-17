using MollyNvr.Application.Discovery;

namespace MollyNvr.Application.Tests;

public class OuiLookupTests
{
    [Theory]
    [InlineData("44:19:B6:AA:BB:CC", "Hikvision")]
    [InlineData("ec-71-db-11-22-33", "REOLINK")]
    [InlineData("3C-EF-8C-01-02-03", "Dahua")]
    public void FindVendor_KnownPrefix_ReturnsVendor(string mac, string expected)
    {
        var lookup = new OuiLookup();

        Assert.Equal(expected, lookup.FindVendor(mac));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-mac")]
    [InlineData("00:00:00:00:00:00")]
    public void FindVendor_UnknownPrefix_ReturnsNull(string? mac)
    {
        var lookup = new OuiLookup();

        Assert.Null(lookup.FindVendor(mac));
    }
}
