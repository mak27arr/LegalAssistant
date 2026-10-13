using LegalAssistant.Logging.DependencyInjection;
using LegalAssistant.WorkerIngest.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddCentralizedLogging(context.Configuration, "worker-ingest");
        services.AddIngestWorkerServices(context.Configuration);
    })
    .Build();

await host.RunAsync();
