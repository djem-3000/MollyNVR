using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MollyNvr.Infrastructure.Storage;

namespace MollyNvr.Infrastructure.Persistence;

/// <summary>Creates the data directory and applies EF Core migrations at application startup.</summary>
public sealed class DatabaseInitializer(
    IDbContextFactory<MollyNvrDbContext> contextFactory,
    StorageOptions storageOptions,
    ILogger<DatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(storageOptions.DataDirectory);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

                logger.LogInformation("Camera database is ready: {DatabasePath}", storageOptions.DatabasePath);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
