using LegalAssistant.Application.Documents.Models;

namespace LegalAssistant.Application.Documents.Services;

public interface IDocumentQueryService
{
    Task<IReadOnlyList<DocumentListItemResult>> GetListAsync(CancellationToken cancellationToken = default);

    Task<DocumentDetailsResult?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
