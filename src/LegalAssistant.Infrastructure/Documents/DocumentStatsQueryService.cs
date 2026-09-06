using LegalAssistant.Application.Documents.Models;
using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Infrastructure.Documents;

public sealed class DocumentStatsQueryService : IDocumentStatsQueryService
{
    private readonly LegalAssistantDbContext _db;

    public DocumentStatsQueryService(LegalAssistantDbContext db)
    {
        _db = db;
    }

    public async Task<DocumentStatsResult> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var totalDocuments = await _db.Documents.CountAsync(d => !d.IsDeleted, cancellationToken);

        var jobStatusCounts = await _db.Jobs
            .GroupBy(job => job.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var counts = jobStatusCounts.ToDictionary(x => x.Status, x => x.Count);

        return new DocumentStatsResult(
            totalDocuments,
            counts.GetValueOrDefault(JobStatus.Queued),
            counts.GetValueOrDefault(JobStatus.InProgress) + counts.GetValueOrDefault(JobStatus.EmbeddingInProgress),
            counts.GetValueOrDefault(JobStatus.Completed),
            counts.GetValueOrDefault(JobStatus.Failed));
    }
}
