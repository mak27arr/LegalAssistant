using System;
using System.Threading;
using System.Threading.Tasks;

namespace LegalAssistant.Application.Embeddings;

public interface IEmbeddingEnqueueService
{
    Task EnqueueEmbeddingAsync(
        Guid chunkId,
        string text,
        Guid? jobId = null,
        Guid? chunkingRunId = null,
        CancellationToken cancellationToken = default);

    Task RequeueEmbeddingAsync(
        Guid chunkId,
        string text,
        Guid? jobId = null,
        Guid? chunkingRunId = null,
        CancellationToken cancellationToken = default);
}
