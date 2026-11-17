using System.Security.Cryptography;
using System.Text;
using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Infrastructure.Media;

/// <summary>Формирование заголовка Authorization для RTSP (Basic и Digest MD5).</summary>
public static class RtspAuthorization
{
    /// <summary>
    /// Строит значение заголовка <c>Authorization</c> по ответу <c>WWW-Authenticate</c>.
    /// Возвращает <c>null</c>, если схема не поддерживается.
    /// </summary>
    public static string? Create(
        string challenge,
        CameraCredentials credentials,
        string method,
        string uri)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        ArgumentNullException.ThrowIfNull(credentials);

        var trimmed = challenge.Trim();

        if (trimmed.StartsWith("Basic", StringComparison.OrdinalIgnoreCase))
        {
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.UserName}:{credentials.Password}"));
            return $"Basic {token}";
        }

        if (!trimmed.StartsWith("Digest", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parameters = ParseParameters(trimmed["Digest".Length..]);
        if (!parameters.TryGetValue("realm", out var realm) || !parameters.TryGetValue("nonce", out var nonce))
        {
            return null;
        }

        parameters.TryGetValue("qop", out var qop);
        parameters.TryGetValue("opaque", out var opaque);
        parameters.TryGetValue("algorithm", out var algorithm);

        var ha1 = Md5($"{credentials.UserName}:{realm}:{credentials.Password}");
        var ha2 = Md5($"{method}:{uri}");
        var cnonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        var nc = "00000001";

        string response;
        if (string.IsNullOrWhiteSpace(qop))
        {
            response = Md5($"{ha1}:{nonce}:{ha2}");
        }
        else
        {
            var selectedQop = qop.Split(',', StringSplitOptions.TrimEntries)
                .FirstOrDefault(static value => value.Equals("auth", StringComparison.OrdinalIgnoreCase))
                ?? qop.Split(',', StringSplitOptions.TrimEntries)[0];

            response = Md5($"{ha1}:{nonce}:{nc}:{cnonce}:{selectedQop}:{ha2}");

            var builder = new StringBuilder()
                .Append("Digest ")
                .Append($"username=\"{credentials.UserName}\", ")
                .Append($"realm=\"{realm}\", ")
                .Append($"nonce=\"{nonce}\", ")
                .Append($"uri=\"{uri}\", ")
                .Append($"response=\"{response}\", ")
                .Append($"qop={selectedQop}, ")
                .Append($"nc={nc}, ")
                .Append($"cnonce=\"{cnonce}\"");

            if (!string.IsNullOrWhiteSpace(algorithm))
            {
                builder.Append($", algorithm={algorithm}");
            }

            if (!string.IsNullOrWhiteSpace(opaque))
            {
                builder.Append($", opaque=\"{opaque}\"");
            }

            return builder.ToString();
        }

        var simple = new StringBuilder()
            .Append("Digest ")
            .Append($"username=\"{credentials.UserName}\", ")
            .Append($"realm=\"{realm}\", ")
            .Append($"nonce=\"{nonce}\", ")
            .Append($"uri=\"{uri}\", ")
            .Append($"response=\"{response}\"");

        if (!string.IsNullOrWhiteSpace(opaque))
        {
            simple.Append($", opaque=\"{opaque}\"");
        }

        return simple.ToString();
    }

    /// <summary>Разбирает параметры challenge вида <c>realm="x", nonce="y", qop="auth"</c>.</summary>
    public static Dictionary<string, string> ParseParameters(string value)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in SplitParameters(value))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
            {
                // Первый токен без «=» — название схемы (Digest/Basic).
                continue;
            }

            var key = part[..separator].Trim();
            var schemeSeparator = key.LastIndexOf(' ');
            if (schemeSeparator >= 0)
            {
                key = key[(schemeSeparator + 1)..];
            }

            var raw = part[(separator + 1)..].Trim();

            if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
            {
                raw = raw[1..^1];
            }

            parameters[key] = raw;
        }

        return parameters;
    }

    private static IEnumerable<string> SplitParameters(string value)
    {
        var start = 0;
        var inQuotes = false;

        for (var index = 0; index < value.Length; index++)
        {
            switch (value[index])
            {
                case '"':
                    inQuotes = !inQuotes;
                    break;
                case ',' when !inQuotes:
                    yield return value[start..index];
                    start = index + 1;
                    break;
            }
        }

        if (start < value.Length)
        {
            yield return value[start..];
        }
    }

    private static string Md5(string value) =>
        Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
