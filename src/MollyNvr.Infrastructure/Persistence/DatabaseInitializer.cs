using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MollyNvr.Infrastructure.Storage;

namespace MollyNvr.Infrastructure.Persistence;

/// <summary>Создаёт каталог данных и применяет миграции EF Core при старте приложения.</summary>
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

        logger.LogInformation("База данных камер готова: {DatabasePath}", storageOptions.DatabasePath);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
