namespace LegalAssistant.Api.Services.Abstractions;

public interface IDocumentService
{
    Task<Guid> CreateDocumentAsync(string title, string url, string content, object metadata, CancellationToken cancellationToken = default);
    Task<LegalAssistant.Domain.Models.Document?> GetDocumentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> UpdateDocumentAsync(Guid id, string title, string content, object metadata, CancellationToken cancellationToken = default);
    Task<bool> DeleteDocumentAsync(Guid id, CancellationToken cancellationToken = default);
}
