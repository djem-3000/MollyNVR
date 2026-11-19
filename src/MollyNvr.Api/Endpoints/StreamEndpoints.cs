using System.Text;
using MollyNvr.Application.Streams;

namespace MollyNvr.Api.Endpoints;

/// <summary>Media stream management: list, add, remove and WebRTC connection (WHEP).</summary>
public static class StreamEndpoints
{
    public static IEndpointRouteBuilder MapStreamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/streams");

        group.MapGet("", ListStreams);
        group.MapPost("", AddStreamAsync);
        group.MapDelete("/{id}", RemoveStreamAsync);
        group.MapPost("/{id}/whep", NegotiateWhepAsync);

        return group;
    }

        /// <summary>List of streams in the registry.</summary>
        /// <remarks>Passwords in source URLs are masked — credentials are never exposed.</remarks>
    private static IResult ListStreams(StreamRegistry registry) => Results.Ok(registry.List());

        /// <summary>Adds a stream to the registry and to go2rtc.</summary>
    /// <remarks>
        /// Body is <see cref="AddStreamRequest"/>: identifier (letters, digits, <c>-</c>, <c>_</c>) and the source URL
        /// (RTSP or an <c>ffmpeg:</c> template). Responses: 201 — stream created; 400 — invalid request;
        /// 409 — a stream with this identifier already exists; 502 — go2rtc rejected the source.
    /// </remarks>
    private static async Task<IResult> AddStreamAsync(
        AddStreamRequest request,
        StreamRegistry registry,
        CancellationToken cancellationToken)
    {
        try
        {
            var stream = await registry.AddAsync(request.Id ?? string.Empty, request.Url, cancellationToken);
            return Results.Created($"/api/streams/{stream.Id}", stream);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (StreamAlreadyExistsException exception)
        {
            return Results.Conflict(new { error = exception.Message });
        }
        catch (MediaEngineException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

        /// <summary>Removes the stream from the registry and from go2rtc.</summary>
        /// <remarks>Responses: 204 — removed; 400 — invalid identifier; 404 — no such stream.</remarks>
    private static async Task<IResult> RemoveStreamAsync(
        string id,
        StreamRegistry registry,
        CancellationToken cancellationToken)
    {
        try
        {
            await registry.RemoveAsync(id, cancellationToken);
            return Results.NoContent();
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (StreamNotFoundException exception)
        {
            return Results.NotFound(new { error = exception.Message });
        }
    }

        /// <summary>WHEP proxy: accepts the browser SDP offer and returns the go2rtc SDP answer.</summary>
    /// <remarks>
        /// The request body is an SDP offer (<c>application/sdp</c>), the response is an SDP answer with the selected codec.
        /// This way only port :9080 is exposed while the go2rtc API stays on loopback.
        /// Responses: 200 — session established; 400 — empty or invalid offer; 404 — no such stream;
        /// 502 — go2rtc rejected the WebRTC request.
    /// </remarks>
    private static async Task<IResult> NegotiateWhepAsync(
        string id,
        HttpRequest request,
        StreamRegistry registry,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        var offer = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(offer))
        {
                        return Results.BadRequest(new { error = "The request body must contain an SDP offer." });
        }

        try
        {
            var answer = await registry.NegotiateAsync(id, offer, cancellationToken);
            return Results.Text(answer, "application/sdp");
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (StreamNotFoundException exception)
        {
            return Results.NotFound(new { error = exception.Message });
        }
        catch (MediaEngineException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }
}

/// <summary>Stream add request: identifier and source URL.</summary>
/// <param name="Id">Stream identifier (letters, digits, <c>-</c>, <c>_</c>); null — 400 error.</param>
/// <param name="Url">Source URL: RTSP or an <c>ffmpeg:</c> template.</param>
public sealed record AddStreamRequest(string? Id, string? Url);
