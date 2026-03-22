using System;
using System.Threading;
using System.Threading.Tasks;

namespace LegalAssistant.Application.Embeddings;

public interface IEmbeddingEnqueueService
{
    Task EnqueueEmbeddingAsync(Guid chunkId, string text, CancellationToken cancellationToken = default);
}
