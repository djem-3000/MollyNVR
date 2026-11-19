using MollyNvr.Application.Archive;
using MollyNvr.Application.Cameras;

namespace MollyNvr.Api.Endpoints;

/// <summary>Recording archive: cameras, calendar, timeline and HLS playback.</summary>
public static class ArchiveEndpoints
{
    public static IEndpointRouteBuilder MapArchiveEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/archive");

        group.MapGet("/cameras", ListCamerasAsync);
        group.MapGet("/cameras/{cameraId:guid}/calendar", GetCalendarAsync);
        group.MapGet("/cameras/{cameraId:guid}/timeline", GetTimelineAsync);
        group.MapPost("/playback", StartPlaybackAsync);
        group.MapDelete("/playback/{sessionId:guid}", StopPlayback);
        group.MapGet("/playback/{sessionId:guid}/{fileName}", GetPlaybackFile);

        return app;
    }

    private static async Task<IResult> ListCamerasAsync(
        ArchiveService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.ListCamerasAsync(cancellationToken));

    private static async Task<IResult> GetCalendarAsync(
        Guid cameraId,
        int? year,
        int? month,
        ArchiveService service,
        CancellationToken cancellationToken)
    {
        var now = DateTime.Now;

        try
        {
            var calendar = await service.GetCalendarAsync(
                cameraId,
                year ?? now.Year,
                month ?? now.Month,
                cancellationToken);

            return Results.Ok(calendar);
        }
        catch (CameraNotFoundException exception)
        {
            return Results.NotFound(new { error = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> GetTimelineAsync(
        Guid cameraId,
        DateOnly? date,
        ArchiveService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var timeline = await service.GetTimelineAsync(
                cameraId,
                date ?? DateOnly.FromDateTime(DateTime.Now),
                cancellationToken);

            return Results.Ok(timeline);
        }
        catch (CameraNotFoundException exception)
        {
            return Results.NotFound(new { error = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> StartPlaybackAsync(
        ArchivePlaybackRequest request,
        ArchiveService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var playback = await service.CreatePlaybackAsync(
                request.CameraId,
                request.From,
                request.SupportsHevc,
                cancellationToken);

            return Results.Ok(playback);
        }
        catch (CameraNotFoundException exception)
        {
            return Results.NotFound(new { error = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static IResult StopPlayback(Guid sessionId, ArchiveService service)
    {
        service.StopPlayback(sessionId);
        return Results.NoContent();
    }

    private static IResult GetPlaybackFile(
        Guid sessionId,
        string fileName,
        ArchiveService service)
    {
        var file = service.FindPlaybackFile(sessionId, fileName);

        return file is null
            ? Results.NotFound()
            : Results.File(file.Content, file.ContentType);
    }
}

/// <summary>Playback request: camera, moment and browser HEVC support.</summary>
public sealed record ArchivePlaybackRequest(
    Guid CameraId,
    DateTimeOffset From,
    bool SupportsHevc);
