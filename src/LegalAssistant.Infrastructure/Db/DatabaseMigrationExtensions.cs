using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Infrastructure.Db;

public static class DatabaseMigrationExtensions
{
    public static async Task ApplyDatabaseMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

        if (dbContext.Database.IsInMemory())
        {
            logger.LogInformation("Skipping database migrations because the in-memory database provider is active.");
            return;
        }

        logger.LogInformation("Applying pending EF Core migrations at API startup.");
        await dbContext.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("EF Core migrations applied successfully.");
    }
}
