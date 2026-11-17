using System.Reflection;
using MollyNvr.Domain.Streams;

namespace MollyNvr.Api.Endpoints;

/// <summary>Системные эндпоинты: проверка живости и сведения о приложении/медиа-ядре.</summary>
public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", GetHealth);
        app.MapGet("/api/system/info", GetSystemInfoAsync);
        return app;
    }

    /// <summary>Проверка живости API (liveness).</summary>
    /// <remarks>Возвращает 200, пока процесс приложения отвечает; зависимости не проверяются.</remarks>
    private static IResult GetHealth() => Results.Ok(new { status = "ok" });

    /// <summary>Версии приложения и .NET, состояние медиа-ядра go2rtc.</summary>
    /// <remarks>
    /// Поле <c>go2rtc</c>: <c>ok</c> — HTTP API go2rtc отвечает, <c>unavailable</c> — не отвечает
    /// (процесс ещё стартует, перезапускается или завершился с ошибкой).
    /// </remarks>
    private static async Task<IResult> GetSystemInfoAsync(
        IMediaEngine mediaEngine,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var go2rtcReady = await IsMediaEngineReadyAsync(mediaEngine, loggerFactory, cancellationToken);

        return Results.Ok(new
        {
            app = "MollyNVR",
            version = typeof(SystemEndpoints).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? typeof(SystemEndpoints).Assembly.GetName().Version?.ToString()
                ?? "dev",
            dotnet = Environment.Version.ToString(),
            go2rtc = go2rtcReady ? "ok" : "unavailable"
        });
    }

    /// <summary>Проверяет готовность go2rtc запросом к его API; ошибки не пробрасываются наружу.</summary>
    private static async Task<bool> IsMediaEngineReadyAsync(
        IMediaEngine mediaEngine,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            await mediaEngine.GetStreamIdsAsync(cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger(nameof(SystemEndpoints)).LogDebug(exception, "go2rtc недоступен");
            return false;
        }
    }
}
