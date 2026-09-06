using LegalAssistant.Messaging;
using Moq;
using RabbitMQ.Client;

namespace LegalAssistant.BackendTests.Messaging;

public sealed class RabbitMqMetadataTests
{
    [Fact]
    public void FromProperties_ShouldCopyCorrelationAndHeaders()
    {
        var properties = new Mock<IBasicProperties>();
        properties.SetupGet(x => x.MessageId).Returns("message-1");
        properties.SetupGet(x => x.CorrelationId).Returns("correlation-1");
        properties.SetupGet(x => x.Type).Returns("test.message");
        properties.SetupGet(x => x.ContentType).Returns("application/json");
        properties.SetupGet(x => x.Persistent).Returns(true);
        properties.SetupGet(x => x.Headers).Returns(new Dictionary<string, object>
        {
            [RabbitMqCorrelation.HeaderName] = "correlation-1"
        });

        var metadata = RabbitMqMessageMetadata.FromProperties(properties.Object);
        var headers = metadata.CopyHeaders();

        Assert.Equal("message-1", metadata.MessageId);
        Assert.Equal("correlation-1", metadata.CorrelationId);
        Assert.Equal("test.message", metadata.MessageType);
        Assert.Equal("correlation-1", RabbitMqCorrelation.TryGetCorrelationId(headers));

        headers["new-header"] = "value";
        Assert.DoesNotContain("new-header", metadata.Headers!.Keys);
    }

    [Fact]
    public void SetCorrelationId_ShouldSetHeaderValue()
    {
        var headers = new Dictionary<string, object>();

        RabbitMqCorrelation.SetCorrelationId(headers, "correlation-2");

        Assert.Equal("correlation-2", RabbitMqCorrelation.TryGetCorrelationId(headers));
    }
}
