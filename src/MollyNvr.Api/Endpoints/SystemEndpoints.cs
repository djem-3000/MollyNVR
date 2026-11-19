using System.Reflection;
using MollyNvr.Domain.Streams;

namespace MollyNvr.Api.Endpoints;

/// <summary>System endpoints: liveness probe and application/media engine details.</summary>
public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", GetHealth);
        app.MapGet("/api/system/info", GetSystemInfoAsync);
        return app;
    }

        /// <summary>API liveness probe.</summary>
        /// <remarks>Returns 200 while the application process responds; dependencies are not checked.</remarks>
    private static IResult GetHealth() => Results.Ok(new { status = "ok" });

        /// <summary>Application and .NET versions, go2rtc media engine state.</summary>
    /// <remarks>
        /// The <c>go2rtc</c> field: <c>ok</c> — the go2rtc HTTP API responds, <c>unavailable</c> — no response
        /// (the process is still starting, restarting or has exited with an error).
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

        /// <summary>Checks go2rtc readiness with an API request; errors are not propagated to the client.</summary>
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
                        loggerFactory.CreateLogger(nameof(SystemEndpoints)).LogDebug(exception, "go2rtc is unavailable");
            return false;
        }
    }
}
