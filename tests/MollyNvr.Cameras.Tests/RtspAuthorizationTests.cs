using MollyNvr.Cameras.Abstractions;
using MollyNvr.Infrastructure.Media;

namespace MollyNvr.Cameras.Tests;

public class RtspAuthorizationTests
{
    private static readonly CameraCredentials Credentials = new("admin", "secret");

    [Fact]
    public void Create_BuildsBasicHeader()
    {
        var header = RtspAuthorization.Create(
            "Basic realm=\"camera\"",
            Credentials,
            "DESCRIBE",
            "rtsp://192.168.1.10:554/main");

        Assert.Equal("Basic YWRtaW46c2VjcmV0", header);
    }

    [Fact]
    public void Create_BuildsDigestHeaderWithQop()
    {
        var header = RtspAuthorization.Create(
            "Digest realm=\"BC Streaming Media\", nonce=\"3e5067483fedea957cdb0ae7ed5dc85a\", qop=\"auth\"",
            Credentials,
            "DESCRIBE",
            "rtsp://192.168.1.113:554/Preview_01_main");

        Assert.NotNull(header);
        Assert.StartsWith("Digest ", header, StringComparison.Ordinal);
        Assert.Contains("username=\"admin\"", header, StringComparison.Ordinal);
        Assert.Contains("realm=\"BC Streaming Media\"", header, StringComparison.Ordinal);
        Assert.Contains("qop=auth", header, StringComparison.Ordinal);
        Assert.Contains("nc=00000001", header, StringComparison.Ordinal);
        Assert.Contains("uri=\"rtsp://192.168.1.113:554/Preview_01_main\"", header, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_BuildsDigestHeaderWithoutQop()
    {
        var header = RtspAuthorization.Create(
            "Digest realm=\"camera\", nonce=\"abc\"",
            Credentials,
            "DESCRIBE",
            "rtsp://192.168.1.10/main");

        Assert.NotNull(header);
        Assert.DoesNotContain("qop=", header, StringComparison.Ordinal);
        Assert.Contains("response=", header, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ReturnsNullForUnknownScheme()
    {
        Assert.Null(RtspAuthorization.Create("Bearer token", Credentials, "DESCRIBE", "rtsp://host/main"));
    }

    [Fact]
    public void ParseParameters_HandlesQuotedCommas()
    {
        var parameters = RtspAuthorization.ParseParameters("Digest realm=\"a, b\", nonce=\"n\", qop=\"auth\"");

        Assert.Equal("a, b", parameters["realm"]);
        Assert.Equal("n", parameters["nonce"]);
        Assert.Equal("auth", parameters["qop"]);
    }
}
