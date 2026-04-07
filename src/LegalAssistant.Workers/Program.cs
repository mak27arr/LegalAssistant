using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
// Using built-in logging and FileLoggerProvider registered in AddCentralizedLogging
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using LegalAssistant.Application.Embeddings;
using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Infrastructure.Documents;
using LegalAssistant.Application.Persistence;
using LegalAssistant.Application.Documents;
using LegalAssistant.Application.Jobs;
using LegalAssistant.Application.Chunks;
using LegalAssistant.Infrastructure.Db;
using LegalAssistant.Infrastructure.Jobs;
using LegalAssistant.Infrastructure.Chunks;
using LegalAssistant.Application.Common;
using LegalAssistant.Infrastructure.Common;
using LegalAssistant.Workers.DependencyInjection;
using LegalAssistant.Logging.DependencyInjection;
using LegalAssistant.Infrastructure.DependencyInjection;
using LegalAssistant.Application.Jobs.Services;
using LegalAssistant.Application.Jobs;
using LegalAssistant.Application.Rag.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LegalAssistant.Infrastructure.Messaging;
using LegalAssistant.Application.Chunking.Services;
using LegalAssistant.Infrastructure.Chunking;

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
