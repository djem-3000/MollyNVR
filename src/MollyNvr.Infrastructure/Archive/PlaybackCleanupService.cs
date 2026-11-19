using Microsoft.Extensions.Hosting;

namespace MollyNvr.Infrastructure.Archive;

/// <summary>Periodically removes finished and idle playback sessions.</summary>
public sealed class PlaybackCleanupService(PlaybackSessionManager manager) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                manager.Cleanup();
            }
        }
        catch (OperationCanceledException)
        {
                        // application shutdown
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        manager.Cleanup();
        await base.StopAsync(cancellationToken);
    }
}
