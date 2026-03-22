using System.Threading;
using System.Threading.Tasks;

namespace LegalAssistant.Application.Messaging;

public interface IIngestMessageConsumer
{
    Task StartAsync(CancellationToken stoppingToken);
}
