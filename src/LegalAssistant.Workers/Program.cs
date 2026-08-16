using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using LegalAssistant.Infrastructure.Db;
using LegalAssistant.Workers.DependencyInjection;
using LegalAssistant.Logging.DependencyInjection;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddLogging(logging =>
        {
            logging.AddFilter((category, level) =>
                category == "Microsoft.EntityFrameworkCore.Database.Command" ? level >= LogLevel.Warning : true);
        });

        // provide service name so logs go to separate per-service files
        services.AddCentralizedLogging(context.Configuration, "workers");

        services.AddWorkerInfrastructure(context.Configuration);
    })
    .Build();

using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();
    dbContext.Database.Migrate();
}

await host.RunAsync();
