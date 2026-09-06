using System.Text.Json;
using LegalAssistant.Application.Documents.Models;
using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Domain.Models;
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

    public async Task<DocumentListPageResult> GetListAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var baseQuery = _db.Documents
            .AsNoTracking()
            .Where(d => !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAt);

        var totalItems = await baseQuery.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var effectivePage = totalPages == 0 ? 1 : Math.Min(page, totalPages);

        var documents = await baseQuery
            .Skip((effectivePage - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DocumentListItemResult(
                d.Id,
                d.Title,
                d.Url,
                d.Version,
                d.CreatedAt,
                d.UpdatedAt,
                d.Chunks.Count,
                null))
            .ToListAsync(cancellationToken);

        if (documents.Count > 0)
        {
            var statuses = await LoadLatestProcessingStatusesAsync(documents.Select(x => x.Id).ToArray(), cancellationToken);
            documents = documents
                .Select(document => document with
                {
                    ProcessingStatus = statuses.GetValueOrDefault(document.Id)
                })
                .ToList();
        }

        return new DocumentListPageResult(
            documents,
            effectivePage,
            pageSize,
            totalItems,
            totalPages,
            totalPages > 0 && effectivePage < totalPages,
            totalPages > 0 && effectivePage > 1);
    }

    public async Task<DocumentDetailsResult?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await _db.Documents
            .AsNoTracking()
            .Where(d => d.Id == id && !d.IsDeleted)
            .Select(d => new
            {
                d.Id,
                d.Title,
                d.Url,
                d.Version,
                d.CreatedAt,
                d.UpdatedAt,
                d.ActiveChunkingRunId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (document is null)
            return null;

        var chunks = _db.DocumentChunks
            .AsNoTracking()
            .Where(c => c.DocumentId == id);
        if (document.ActiveChunkingRunId is Guid runId)
            chunks = chunks.Where(c => c.ChunkingRunId == runId);

        var chunkSummary = await chunks
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Completed = group.Count(c => c.EmbeddingStatus == LegalAssistant.Domain.Models.EmbeddingStatus.Completed && c.Embedding != null),
                Failed = group.Count(c => c.EmbeddingStatus == LegalAssistant.Domain.Models.EmbeddingStatus.Failed)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var status = await GetLatestProcessingStatusAsync(id, cancellationToken);
        return new DocumentDetailsResult(
            document.Id,
            document.Title,
            document.Url,
            document.Version,
            document.CreatedAt,
            document.UpdatedAt,
            chunkSummary?.Total ?? 0,
            status,
            chunkSummary?.Total ?? 0,
            chunkSummary?.Completed ?? 0,
            chunkSummary?.Failed ?? 0);
    }

    private async Task<Dictionary<Guid, string>> LoadLatestProcessingStatusesAsync(IReadOnlyCollection<Guid> documentIds, CancellationToken cancellationToken)
    {
        var remainingDocumentIds = documentIds.ToHashSet();
        var statuses = new Dictionary<Guid, string>();

        var ingestJobs = await _db.Jobs
            .AsNoTracking()
            .Where(j => j.Type == "ingest")
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new { j.Payload, j.Status })
            .ToListAsync(cancellationToken);

        foreach (var job in ingestJobs)
        {
            var documentId = TryReadDocumentId(job.Payload);
            if (documentId is null || !remainingDocumentIds.Remove(documentId.Value))
                continue;

            statuses[documentId.Value] = job.Status.ToString();
            if (remainingDocumentIds.Count == 0)
                break;
        }

        return statuses;
    }

    private async Task<string?> GetLatestProcessingStatusAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var jobs = await _db.Jobs
            .AsNoTracking()
            .Where(j => j.Type == "ingest")
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new { j.Payload, j.Status })
            .ToListAsync(cancellationToken);

        foreach (var job in jobs)
        {
            if (TryReadDocumentId(job.Payload) == documentId)
                return job.Status.ToString();
        }

        return null;
    }

    private static Guid? TryReadDocumentId(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("DocumentId", out var property))
                return null;

            var rawDocumentId = property.GetString();
            return Guid.TryParse(rawDocumentId, out var documentId) ? documentId : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
