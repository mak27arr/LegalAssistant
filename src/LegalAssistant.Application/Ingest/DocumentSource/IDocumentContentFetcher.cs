using System.Threading;
using System.Threading.Tasks;

namespace LegalAssistant.Application.Documents.Services;

public interface IDocumentContentFetcher
{
    Task<string?> FetchPlainTextAsync(string url, CancellationToken cancellationToken = default);
}
