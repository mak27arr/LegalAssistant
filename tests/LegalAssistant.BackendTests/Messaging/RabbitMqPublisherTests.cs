using LegalAssistant.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RabbitMQ.Client;

namespace LegalAssistant.BackendTests.Messaging;

public sealed class RabbitMqPublisherTests
{
    [Fact]
    public async Task PublishAsync_ShouldUsePersistentPropertiesAndWaitForConfirm()
    {
        var model = CreateModel(waitForConfirm: true, out var properties);
        var connection = new Mock<IConnection>();
        connection.SetupGet(x => x.IsOpen).Returns(true);
        connection.Setup(x => x.CreateModel()).Returns(model.Object);

        var provider = new Mock<IRabbitMqConnectionProvider>();
        provider.Setup(x => x.GetConnection(It.IsAny<CancellationToken>())).Returns(connection.Object);

        using var publisher = new RabbitMqPublisher(provider.Object, NullLogger<RabbitMqPublisher>.Instance);

        await publisher.PublishAsync(
            new RabbitMqPublishAddress("events", "event.created"),
            new { Value = 42 },
            new RabbitMqMessageMetadata
            {
                MessageId = "message-1",
                CorrelationId = "correlation-1",
                MessageType = "event.created"
            });

        Assert.True(properties.Object.Persistent);
        Assert.Equal("message-1", properties.Object.MessageId);
        Assert.Equal("correlation-1", properties.Object.CorrelationId);
        model.Verify(x => x.ConfirmSelect(), Times.Once);
        model.Verify(x => x.WaitForConfirms(), Times.Once);
        model.Verify(x => x.BasicPublish(
            "events",
            "event.created",
            false,
            It.IsAny<IBasicProperties>(),
            It.IsAny<ReadOnlyMemory<byte>>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_ShouldThrowWhenBrokerDoesNotConfirm()
    {
        var model = CreateModel(waitForConfirm: false, out _);
        var connection = new Mock<IConnection>();
        connection.SetupGet(x => x.IsOpen).Returns(true);
        connection.Setup(x => x.CreateModel()).Returns(model.Object);

        var provider = new Mock<IRabbitMqConnectionProvider>();
        provider.Setup(x => x.GetConnection(It.IsAny<CancellationToken>())).Returns(connection.Object);

        using var publisher = new RabbitMqPublisher(provider.Object, NullLogger<RabbitMqPublisher>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.PublishAsync(
            new RabbitMqPublishAddress(string.Empty, "queue"),
            new { Value = 1 },
            new RabbitMqMessageMetadata()));
    }

    private static Mock<IModel> CreateModel(bool waitForConfirm, out Mock<IBasicProperties> properties)
    {
        properties = new Mock<IBasicProperties>();
        properties.SetupProperty(x => x.Persistent);
        properties.SetupProperty(x => x.MessageId);
        properties.SetupProperty(x => x.CorrelationId);
        properties.SetupProperty(x => x.Type);
        properties.SetupProperty(x => x.ContentType);
        properties.SetupProperty(x => x.Expiration);
        properties.SetupProperty(x => x.Headers);
        var model = new Mock<IModel>();
        model.SetupGet(x => x.IsOpen).Returns(true);
        model.Setup(x => x.CreateBasicProperties()).Returns(properties.Object);
        model.Setup(x => x.WaitForConfirms()).Returns(waitForConfirm);
        return model;
    }
}
