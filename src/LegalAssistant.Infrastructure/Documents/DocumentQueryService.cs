using LegalAssistant.Application.Documents.Models;
using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Infrastructure.Documents;

public sealed class DocumentQueryService : IDocumentQueryService
{
    private readonly LegalAssistantDbContext _db;

    public DocumentQueryService(LegalAssistantDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DocumentListItemResult>> GetListAsync(CancellationToken cancellationToken = default)
        => await _db.Documents
            .AsNoTracking()
            .Where(d => !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DocumentListItemResult(
                d.Id,
                d.Title,
                d.Url,
                d.Version,
                d.CreatedAt,
                d.UpdatedAt,
                d.Chunks.Count))
            .ToListAsync(cancellationToken);

    public async Task<DocumentDetailsResult?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.Documents
            .AsNoTracking()
            .Where(d => d.Id == id && !d.IsDeleted)
            .Select(d => new DocumentDetailsResult(
                d.Id,
                d.Title,
                d.Url,
                d.Version,
                d.CreatedAt,
                d.UpdatedAt,
                d.Chunks.Count))
            .FirstOrDefaultAsync(cancellationToken);
}
