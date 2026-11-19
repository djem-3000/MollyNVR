using System.Net;

namespace MollyNvr.Cameras.Abstractions.Http;

/// <summary>
/// Creation of HTTP clients for camera requests. Credentials are passed to the handler,
/// which performs Digest/Basic authentication itself (HTTP 401 + request retry).
/// </summary>
public static class CameraHttpClientFactory
{
        /// <summary>Creates a client with the camera base address and (optionally) credentials.</summary>
    public static HttpClient Create(Uri baseAddress, CameraCredentials? credentials, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);

        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(3),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            PreAuthenticate = false,
        };

        if (credentials is not null)
        {
            handler.Credentials = new NetworkCredential(credentials.UserName, credentials.Password);
        }

        var client = new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = baseAddress,
            Timeout = timeout,
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("MollyNvr/1.0");

        return client;
    }
}
