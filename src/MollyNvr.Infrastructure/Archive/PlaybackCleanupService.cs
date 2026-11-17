using Microsoft.Extensions.Hosting;

namespace MollyNvr.Infrastructure.Archive;

/// <summary>Периодически убирает завершённые и простаивающие playback-сессии.</summary>
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
            // остановка приложения
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        manager.Cleanup();
        await base.StopAsync(cancellationToken);
    }
}
