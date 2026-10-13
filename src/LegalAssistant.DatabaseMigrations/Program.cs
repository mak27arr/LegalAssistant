using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
using var host = builder.Build();
var logger = host.Services
    .GetRequiredService<ILoggerFactory>()
    .CreateLogger("DatabaseMigrations");

logger.LogInformation("Database migration job started.");

try
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required for the migration job.");

    var options = new DbContextOptionsBuilder<LegalAssistantDbContext>()
        .UseNpgsql(connectionString, postgres => postgres.UseVector())
        .UseLoggerFactory(host.Services.GetRequiredService<ILoggerFactory>())
        .EnableDetailedErrors()
        .Options;

    await using var db = new LegalAssistantDbContext(options);
    var pendingMigrations = (await db.Database.GetPendingMigrationsAsync()).ToArray();
    logger.LogInformation(
        "Found {PendingMigrationCount} pending database migrations: {PendingMigrationIds}",
        pendingMigrations.Length,
        pendingMigrations.Length == 0 ? "none" : string.Join(", ", pendingMigrations));

    logger.LogInformation("Applying database migrations.");
    await db.Database.MigrateAsync();
    logger.LogInformation("Database migrations completed successfully.");
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Database migration job failed.");
    throw;
}
