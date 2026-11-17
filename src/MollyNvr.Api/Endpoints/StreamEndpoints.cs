using System.Text;
using MollyNvr.Application.Streams;

namespace MollyNvr.Api.Endpoints;

/// <summary>Управление медиа-потоками: список, добавление, удаление и WebRTC-подключение (WHEP).</summary>
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

    /// <summary>Список потоков реестра.</summary>
    /// <remarks>Пароли в URL источников маскируются — наружу креды не отдаются.</remarks>
    private static IResult ListStreams(StreamRegistry registry) => Results.Ok(registry.List());

    /// <summary>Добавляет поток в реестр и в go2rtc.</summary>
    /// <remarks>
    /// Тело — <see cref="AddStreamRequest"/>: идентификатор (латиница, цифры, <c>-</c>, <c>_</c>) и URL источника
    /// (RTSP или шаблон <c>ffmpeg:</c>). Ответы: 201 — поток создан; 400 — некорректный запрос;
    /// 409 — поток с таким идентификатором уже есть; 502 — go2rtc отклонил источник.
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

    /// <summary>Удаляет поток из реестра и из go2rtc.</summary>
    /// <remarks>Ответы: 204 — удалён; 400 — некорректный идентификатор; 404 — потока нет.</remarks>
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

    /// <summary>WHEP-прокси: принимает SDP offer браузера и возвращает SDP answer go2rtc.</summary>
    /// <remarks>
    /// Тело запроса — SDP offer (<c>application/sdp</c>), ответ — SDP answer с выбранным кодеком.
    /// Так наружу открыт только порт :9080, а API go2rtc остаётся на loopback.
    /// Ответы: 200 — сессия установлена; 400 — пустой/некорректный offer; 404 — потока нет;
    /// 502 — go2rtc отклонил WebRTC-запрос.
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
            return Results.BadRequest(new { error = "Тело запроса должно содержать SDP offer." });
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

/// <summary>Запрос на добавление потока: идентификатор и URL источника.</summary>
/// <param name="Id">Идентификатор потока (латиница, цифры, <c>-</c>, <c>_</c>); null — ошибка 400.</param>
/// <param name="Url">URL источника: RTSP или шаблон <c>ffmpeg:</c>.</param>
public sealed record AddStreamRequest(string? Id, string? Url);
