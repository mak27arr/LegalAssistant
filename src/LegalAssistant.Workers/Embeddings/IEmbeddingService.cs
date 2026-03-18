using System.Threading;
using System.Threading.Tasks;

namespace LegalAssistant.Workers.Embeddings
{
    public interface IEmbeddingService
    {
        Task EnqueueEmbeddingAsync(Guid chunkId, string text, CancellationToken cancellationToken = default);
    }
}
