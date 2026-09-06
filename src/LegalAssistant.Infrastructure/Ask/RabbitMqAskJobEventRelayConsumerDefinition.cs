using System.Text.Json;
using LegalAssistant.Application.Ask;
using LegalAssistant.Domain.Models;
using LegalAssistant.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Infrastructure.Ask;

public sealed class RabbitMqAskJobEventRelayConsumerDefinition : IRabbitMqConsumerDefinition<AskJobEventRecord>
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<RabbitMqAskJobEventRelayConsumerDefinition> _logger;

    public RabbitMqAskJobEventRelayConsumerDefinition(ILogger<RabbitMqAskJobEventRelayConsumerDefinition> logger)
    {
        _logger = logger;
    }

    public RabbitMqConsumerEndpoint Endpoint { get; } = new(string.Empty)
    {
        ExchangeName = AskJobRabbitMqTopology.Exchange,
        ExchangeType = "topic",
        BindingRoutingKey = "ask.job.*",
        Durable = false,
        Exclusive = true,
        AutoDelete = true,
        PrefetchCount = 50,
        DeclareRetryQueue = false,
        DeadLetter = new RabbitMqDeadLetterDefinition(
            "ask:relay:dlx",
            "ask:relay:dlq",
            "ask:relay")
    };

    public AskJobEventRecord Deserialize(ReadOnlyMemory<byte> body) => JsonSerializer.Deserialize<AskJobEventRecord>(body.Span, SerializerOptions)
           ?? throw new JsonException("Ask event payload was empty.");

    public async Task<RabbitMqMessageResult> HandleAsync(
        IServiceProvider scopedServices,
        RabbitMqMessageContext<AskJobEventRecord> context,
        CancellationToken cancellationToken)
    {
        try
        {
            var fanout = scopedServices.GetRequiredService<IAskJobEventFanout>();
            await fanout.PublishAsync(context.Message, cancellationToken);
            return RabbitMqMessageResult.Ack;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Ask event fanout failed; dead-lettering without acknowledging. jobId={JobId} eventId={EventId}",
                context.Message.JobId,
                context.Message.Id);
            return RabbitMqMessageResult.DeadLetter;
        }
    }
}
