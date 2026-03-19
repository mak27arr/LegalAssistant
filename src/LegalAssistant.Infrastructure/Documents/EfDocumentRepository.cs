using LegalAssistant.Application.Documents;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Infrastructure.Documents;

public sealed class EfDocumentRepository : IDocumentRepository
{
    private readonly LegalAssistantDbContext _db;

    public EfDocumentRepository(LegalAssistantDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(Document document, CancellationToken cancellationToken = default)
        => _db.Documents.AddAsync(document, cancellationToken).AsTask();

    public Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.Documents.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, cancellationToken);

    public Task<Document?> GetByIdWithChunksAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.Documents.Include(d => d.Chunks).FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, cancellationToken);

    public void Update(Document document) => _db.Documents.Update(document);
}
