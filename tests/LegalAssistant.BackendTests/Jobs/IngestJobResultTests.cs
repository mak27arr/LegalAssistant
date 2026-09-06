using System.Text.Json;
using LegalAssistant.Application.Jobs.Models;
using LegalAssistant.Domain.Models;

namespace LegalAssistant.BackendTests.Jobs;

public sealed class IngestJobResultTests
{
    [Fact]
    public void Serialize_ShouldWriteEmbeddingStatusAsCamelCaseString()
    {
        var json = IngestJobResultSerializer.Serialize(3, EmbeddingStatus.Pending);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(3, document.RootElement.GetProperty("chunks").GetInt32());
        Assert.Equal("pending", document.RootElement.GetProperty("embeddings").GetString());
    }
}
