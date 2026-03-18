using System.Threading;
using System.Threading.Tasks;

namespace LegalAssistant.Domain.Documents;

public interface IDocumentContentFetcher
{
    Task<string?> FetchPlainTextAsync(string url, CancellationToken cancellationToken = default);
}
