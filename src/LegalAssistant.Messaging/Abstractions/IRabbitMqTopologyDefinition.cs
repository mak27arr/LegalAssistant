namespace LegalAssistant.Messaging;

public interface IRabbitMqTopologyDefinition
{
    void Declare(RabbitMqTopologyBuilder topology);
}
