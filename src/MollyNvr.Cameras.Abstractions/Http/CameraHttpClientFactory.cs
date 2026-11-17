using System.Net;

namespace MollyNvr.Cameras.Abstractions.Http;

/// <summary>
/// Создание HTTP-клиентов для запросов к камере. Учётные данные передаются обработчику,
/// который сам проходит Digest/Basic-аутентификацию (HTTP 401 + повтор запроса).
/// </summary>
public static class CameraHttpClientFactory
{
    /// <summary>Создаёт клиент с базовым адресом камеры и (опционально) учётными данными.</summary>
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
