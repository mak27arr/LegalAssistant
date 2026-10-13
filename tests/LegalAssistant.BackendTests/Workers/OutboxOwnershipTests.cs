using LegalAssistant.Application.Messaging;

namespace LegalAssistant.BackendTests.Workers;

public sealed class OutboxOwnershipTests
{
    [Theory]
    [InlineData(DocumentIngestMessageNames.MessageType, OutboxMessageOwners.Ingest)]
    [InlineData(EmbeddingRequestMessageNames.MessageType, OutboxMessageOwners.Ingest)]
    [InlineData(EmbeddingCompletedMessageNames.MessageType, OutboxMessageOwners.Embeddings)]
    [InlineData("ask.job.queued", OutboxMessageOwners.Ask)]
    [InlineData("ask.job.inprogress", OutboxMessageOwners.Ask)]
    [InlineData("ask.job.completed", OutboxMessageOwners.Ask)]
    [InlineData("ask.job.failed", OutboxMessageOwners.Ask)]
    public void MessageType_HasExactlyOneOwner(string messageType, string expectedOwner)
    {
        Assert.Equal(expectedOwner, OutboxMessageOwners.ForMessageType(messageType));
    }

    [Fact]
    public void AllKnownMessageTypes_AreUnique()
    {
        var messageTypes = OutboxMessageOwners.AllMessageTypes;

        Assert.Equal(
            messageTypes.Count,
            messageTypes.Distinct(StringComparer.Ordinal).Count());
    }
}
