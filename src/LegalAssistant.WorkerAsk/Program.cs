using LegalAssistant.Logging.DependencyInjection;
using LegalAssistant.WorkerAsk.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddCentralizedLogging(context.Configuration, "worker-ask");
        services.AddAskWorkerServices(context.Configuration);
    })
    .Build();

await host.RunAsync();
