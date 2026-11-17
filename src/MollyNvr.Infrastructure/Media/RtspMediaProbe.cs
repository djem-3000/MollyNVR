using System.Globalization;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using MollyNvr.Application.Cameras;
using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Infrastructure.Media;

/// <summary>Определяет фактические кодеки RTSP-потока через DESCRIBE и SDP.</summary>
public sealed class RtspMediaProbe(ILogger<RtspMediaProbe> logger) : IRtspMediaProbe
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(6);

    /// <inheritdoc />
    public async Task<RtspMediaInfo?> ProbeAsync(
        Uri rtspUri,
        CameraCredentials credentials,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rtspUri);
        ArgumentNullException.ThrowIfNull(credentials);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(rtspUri.Host, rtspUri.Port, timeout.Token);
            await using var stream = client.GetStream();

            // Некоторые камеры привязывают nonce к соединению: повторный DESCRIBE
            // с авторизацией должен идти в том же TCP-соединении.
            var first = await DescribeAsync(stream, rtspUri, authorization: null, cseq: 1, timeout.Token);
            if (first.StatusCode == 200)
            {
                return RtspSdpParser.Parse(first.Body);
            }

            if (first.StatusCode != 401 || first.Challenge is null)
            {
                return null;
            }

            var authorization = RtspAuthorization.Create(first.Challenge, credentials, "DESCRIBE", rtspUri.AbsoluteUri);
            if (authorization is null)
            {
                return null;
            }

            var second = await DescribeAsync(stream, rtspUri, authorization, cseq: 2, timeout.Token);
            return second.StatusCode == 200 ? RtspSdpParser.Parse(second.Body) : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(
                "RTSP DESCRIBE не удался для {Host}:{Port}: {Error}",
                rtspUri.Host,
                rtspUri.Port,
                exception.Message);

            return null;
        }
    }

    private static async Task<RtspResponse> DescribeAsync(
        NetworkStream stream,
        Uri rtspUri,
        string? authorization,
        int cseq,
        CancellationToken cancellationToken)
    {
        var request = new StringBuilder()
            .Append("DESCRIBE ").Append(rtspUri.AbsoluteUri).Append(" RTSP/1.0\r\n")
            .Append("CSeq: ").Append(cseq).Append("\r\n")
            .Append("Accept: application/sdp\r\n")
            .Append("User-Agent: MollyNvr/1.0\r\n");

        if (authorization is not null)
        {
            request.Append("Authorization: ").Append(authorization).Append("\r\n");
        }

        request.Append("\r\n");

        await stream.WriteAsync(Encoding.ASCII.GetBytes(request.ToString()), cancellationToken);
        await stream.FlushAsync(cancellationToken);

        var (head, leftover) = await ReadHeadAsync(stream, cancellationToken);
        var (statusCode, headers) = ParseHead(head);

        var body = string.Empty;
        if (headers.TryGetValue("Content-Length", out var lengthText) &&
            int.TryParse(lengthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var length) &&
            length > 0)
        {
            var buffer = new byte[length];
            var copied = Math.Min(leftover.Length, length);
            leftover.AsSpan(0, copied).CopyTo(buffer);

            if (copied < length)
            {
                await stream.ReadExactlyAsync(buffer.AsMemory(copied), cancellationToken);
            }

            body = Encoding.UTF8.GetString(buffer);
        }

        headers.TryGetValue("WWW-Authenticate", out var challenge);

        return new RtspResponse(statusCode, body, challenge);
    }

    private static async Task<(string Head, byte[] Leftover)> ReadHeadAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var total = 0;
        var headEnd = -1;

        while (headEnd < 0 && total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            headEnd = IndexOfHeadEnd(buffer, total);
        }

        if (headEnd < 0)
        {
            return (Encoding.ASCII.GetString(buffer, 0, total), []);
        }

        var head = Encoding.ASCII.GetString(buffer, 0, headEnd);
        var leftover = buffer.AsSpan(headEnd + 4, total - headEnd - 4).ToArray();

        return (head, leftover);
    }

    private static int IndexOfHeadEnd(byte[] buffer, int length)
    {
        for (var index = 0; index + 3 < length; index++)
        {
            if (buffer[index] == '\r' && buffer[index + 1] == '\n' &&
                buffer[index + 2] == '\r' && buffer[index + 3] == '\n')
            {
                return index;
            }
        }

        return -1;
    }

    private static (int StatusCode, Dictionary<string, string> Headers) ParseHead(string head)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = head.Split("\r\n");

        var statusCode = 0;
        if (lines.Length > 0)
        {
            var parts = lines[0].Split(' ', 3);
            if (parts.Length >= 2)
            {
                int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out statusCode);
            }
        }

        foreach (var line in lines.Skip(1))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }

        return (statusCode, headers);
    }

    private sealed record RtspResponse(int StatusCode, string Body, string? Challenge);
}
