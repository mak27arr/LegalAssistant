using RabbitMQ.Client;

namespace LegalAssistant.Messaging;

public interface IRabbitMqConnectionProvider
{
    IConnection GetConnection(CancellationToken cancellationToken = default);

    void Reset();
}
