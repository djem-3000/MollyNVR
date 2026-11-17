using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MollyNvr.Infrastructure.Persistence;

/// <summary>Фабрика контекста для команд <c>dotnet ef</c> (миграции создаются без запуска приложения).</summary>
internal sealed class MollyNvrDbContextFactory : IDesignTimeDbContextFactory<MollyNvrDbContext>
{
    public MollyNvrDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<MollyNvrDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options);
}
