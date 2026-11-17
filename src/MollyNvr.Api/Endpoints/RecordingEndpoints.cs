using MollyNvr.Application.Cameras;
using MollyNvr.Application.Recording;

namespace MollyNvr.Api.Endpoints;

/// <summary>Настройки записи камер и общего пула хранения.</summary>
public static class RecordingEndpoints
{
    public static IEndpointRouteBuilder MapRecordingEndpoints(this IEndpointRouteBuilder app)
    {
        var recording = app.MapGroup("/api/cameras/{id:guid}/recording");
        recording.MapGet("", GetAsync);
        recording.MapPut("", SaveAsync);

        var storage = app.MapGroup("/api/settings/storage");
        storage.MapGet("", GetStorageAsync);
        storage.MapPut("", SaveStorageAsync);

        app.MapGet("/api/recording/status", GetLiveStatusAsync);

        return app;
    }

    private static async Task<IResult> GetLiveStatusAsync(
        RecordingLiveStatusService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.GetAsync(cancellationToken));

    private static async Task<IResult> GetAsync(
        Guid id,
        RecordingService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.GetAsync(id, cancellationToken));
        }
        catch (CameraNotFoundException exception)
        {
            return Results.NotFound(new { error = exception.Message });
        }
    }

    private static async Task<IResult> SaveAsync(
        Guid id,
        RecordingSettingsBody body,
        RecordingService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new RecordingSettingsRequest(
                body.Enabled,
                body.ProfileId,
                body.ContinuousEnabled,
                body.ContinuousSchedule,
                body.EventEnabled,
                body.EventSchedule,
                body.EventTypes,
                body.PreRollSeconds,
                body.PostRollSeconds);

            return Results.Ok(await service.SaveAsync(id, request, cancellationToken));
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

    private static async Task<IResult> GetStorageAsync(
        RecordingService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.GetStorageAsync(cancellationToken));

    private static async Task<IResult> SaveStorageAsync(
        StorageSettingsBody body,
        RecordingService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var mode = string.Equals(body.Mode, "watermark", StringComparison.OrdinalIgnoreCase)
                ? StorageOverwriteMode.Watermark
                : StorageOverwriteMode.Fifo;

            var request = new StorageSettingsRequest(
                body.Path,
                body.QuotaPercent,
                mode,
                body.HighWatermarkPercent,
                body.LowWatermarkPercent);

            return Results.Ok(await service.SaveStorageAsync(request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }
}

/// <summary>Тело запроса настроек записи камеры.</summary>
public sealed record RecordingSettingsBody(
    bool Enabled,
    string? ProfileId,
    bool ContinuousEnabled,
    string? ContinuousSchedule,
    bool EventEnabled,
    string? EventSchedule,
    List<string>? EventTypes,
    int PreRollSeconds,
    int PostRollSeconds);

/// <summary>Тело запроса настроек пула.</summary>
public sealed record StorageSettingsBody(
    string? Path,
    int QuotaPercent,
    string? Mode,
    int HighWatermarkPercent,
    int LowWatermarkPercent);
