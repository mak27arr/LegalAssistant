using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LegalAssistant.Infrastructure.Db;

public sealed class LegalAssistantDbContextFactory : IDesignTimeDbContextFactory<LegalAssistantDbContext>
{
    public LegalAssistantDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LegalAssistantDbContext>();

        // Use the same env var name as containers; fallback for local dev.
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                   ?? "Host=localhost;Port=5432;Database=legalassistant;Username=legal;Password=legalpw";

        optionsBuilder.UseNpgsql(conn, o => o.UseVector());
        return new LegalAssistantDbContext(optionsBuilder.Options);
    }
}
