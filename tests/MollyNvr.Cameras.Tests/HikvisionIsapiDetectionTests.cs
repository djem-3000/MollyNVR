using System.Net;
using System.Net.Http.Headers;
using MollyNvr.Cameras.Hikvision;

namespace MollyNvr.Cameras.Tests;

public class HikvisionIsapiDetectionTests
{
    [Fact]
    public void IsIsapiResponse_XmlWithIsapiNamespace_ReturnsTrue()
    {
        const string body = """
            <?xml version="1.0" encoding="UTF-8"?>
            <userCheck version="2.0" xmlns="http://www.isapi.org/ver20/XMLSchema">
            <statusValue>401</statusValue>
            </userCheck>
            """;

        var response = CreateResponse(HttpStatusCode.Unauthorized, body, "application/xml");

        Assert.True(HikvisionCameraModule.IsIsapiResponse(response, body));
    }

    [Fact]
    public void IsIsapiResponse_DigestChallengeWithHikvisionRealm_ReturnsTrue()
    {
        const string body = "<error/>";
        var response = CreateResponse(HttpStatusCode.Unauthorized, body, "application/xml");
        response.Headers.WwwAuthenticate.Add(
            new AuthenticationHeaderValue(
                "Digest",
                "qop=\"auth\", realm=\"DS-CA8C1265\", nonce=\"abc\""));

        Assert.True(HikvisionCameraModule.IsIsapiResponse(response, body));
    }

    [Fact]
    public void IsIsapiResponse_RouterWebPanel_ReturnsFalse()
    {
        const string body = "<!doctype html><html><head><title>KeeneticOS Web Panel</title></head></html>";
        var response = CreateResponse(HttpStatusCode.OK, body, "text/html");

        Assert.False(HikvisionCameraModule.IsIsapiResponse(response, body));
    }

    [Fact]
    public void IsIsapiResponse_NonXmlResponseWithoutMarkers_ReturnsFalse()
    {
        const string body = "Unauthorized";
        var response = CreateResponse(HttpStatusCode.Unauthorized, body, "text/plain");

        Assert.False(HikvisionCameraModule.IsIsapiResponse(response, body));
    }

    private static HttpResponseMessage CreateResponse(
        HttpStatusCode status,
        string body,
        string mediaType)
    {
        var content = new StringContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);

        return new HttpResponseMessage(status) { Content = content };
    }
}
