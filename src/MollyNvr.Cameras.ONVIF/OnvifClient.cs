using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Cameras.ONVIF;

/// <summary>Отправка SOAP-запросов ONVIF и разбор ошибок.</summary>
internal static class OnvifClient
{
    /// <summary>Отправляет запрос и возвращает тело ответа (ошибки HTTP не выбрасываются).</summary>
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

    /// <summary>Выбрасывает исключение, если ответ содержит SOAP Fault.</summary>
    public static void EnsureSuccess(string body, string operation)
    {
        var fault = OnvifSoap.ReadFault(body);
        if (fault is null)
        {
            return;
        }

        if (IsAuthorizationFault(fault))
        {
            throw new CameraAuthenticationException($"ONVIF отклонил учётные данные при операции «{operation}».");
        }

        throw new CameraUnavailableException($"ONVIF: операция «{operation}» завершилась ошибкой: {fault}");
    }

    /// <summary>Проверяет, является ли ошибка отказом в авторизации.</summary>
    public static bool IsAuthorizationFault(string fault) =>
        fault.Contains("NotAuthorized", StringComparison.OrdinalIgnoreCase) ||
        fault.Contains("NotAuthenticated", StringComparison.OrdinalIgnoreCase) ||
        fault.Contains("FailedAuthentication", StringComparison.OrdinalIgnoreCase) ||
        fault.Contains("authorization", StringComparison.OrdinalIgnoreCase) ||
        fault.Contains("authentication", StringComparison.OrdinalIgnoreCase);
}
