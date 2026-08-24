using LegalAssistant.Application.Documents.Models;

namespace LegalAssistant.Application.Documents.Services;

public interface IDocumentQueryService
{
    Task<DocumentListPageResult> GetListAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<DocumentDetailsResult?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
