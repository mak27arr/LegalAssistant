using System.Threading;
using System.Threading.Tasks;

namespace LegalAssistant.Workers.Embeddings
{
    public interface IEmbeddingService
    {
        Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    }
}
