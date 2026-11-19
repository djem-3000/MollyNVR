using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Cameras.ONVIF;

/// <summary>Sending ONVIF SOAP requests and parsing errors.</summary>
internal static class OnvifClient
{
        /// <summary>Sends the request and returns the response body (HTTP errors are not thrown).</summary>
    public static async Task<string> SendAsync(
        HttpClient client,
        Uri serviceUri,
        string action,
        XElement body,
        CameraCredentials? credentials,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(serviceUri);
        ArgumentNullException.ThrowIfNull(body);

        var envelope = OnvifSoap.BuildEnvelope(credentials, body);

        using var content = new StringContent(envelope, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/soap+xml") { CharSet = "utf-8" };
        content.Headers.ContentType.Parameters.Add(new NameValueHeaderValue("action", $"\"{action}\""));

        using var response = await client.PostAsync(serviceUri, content, cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

        /// <summary>Throws when the response contains a SOAP Fault.</summary>
    public static void EnsureSuccess(string body, string operation)
    {
        var fault = OnvifSoap.ReadFault(body);
        if (fault is null)
        {
            return;
        }

        if (IsAuthorizationFault(fault))
        {
                        throw new CameraAuthenticationException($"ONVIF rejected the credentials for the '{operation}' operation.");
        }

                throw new CameraUnavailableException($"ONVIF: the '{operation}' operation failed: {fault}");
    }

        /// <summary>Checks whether the error is an authorization rejection.</summary>
    public static bool IsAuthorizationFault(string fault) =>
        fault.Contains("NotAuthorized", StringComparison.OrdinalIgnoreCase) ||
        fault.Contains("NotAuthenticated", StringComparison.OrdinalIgnoreCase) ||
        fault.Contains("FailedAuthentication", StringComparison.OrdinalIgnoreCase) ||
        fault.Contains("authorization", StringComparison.OrdinalIgnoreCase) ||
        fault.Contains("authentication", StringComparison.OrdinalIgnoreCase);
}
