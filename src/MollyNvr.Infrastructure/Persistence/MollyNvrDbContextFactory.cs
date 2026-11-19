using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MollyNvr.Infrastructure.Persistence;

/// <summary>Context factory for <c>dotnet ef</c> commands (migrations are created without running the app).</summary>
internal sealed class MollyNvrDbContextFactory : IDesignTimeDbContextFactory<MollyNvrDbContext>
{
    public MollyNvrDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<MollyNvrDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options);
}
