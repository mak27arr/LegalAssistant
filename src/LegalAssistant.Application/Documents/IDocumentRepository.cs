using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Documents;

public interface IDocumentRepository
{
    Task AddAsync(Document document, CancellationToken cancellationToken = default);
    Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Document?> GetByIdWithChunksAsync(Guid id, CancellationToken cancellationToken = default);
    void Update(Document document);
}
