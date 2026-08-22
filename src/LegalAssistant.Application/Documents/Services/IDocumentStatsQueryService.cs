using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Application.Documents.Models;

namespace LegalAssistant.Application.Documents.Services;

public interface IDocumentStatsQueryService
{
    Task<DocumentStatsResult> GetStatsAsync(CancellationToken cancellationToken = default);
}
