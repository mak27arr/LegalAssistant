using System.Text.Json;
using LegalAssistant.Api.Messaging;
using LegalAssistant.Api.Services.Abstractions;
using LegalAssistant.Application.Common;
using LegalAssistant.Application.Documents;
using LegalAssistant.Application.Jobs;
using LegalAssistant.Application.Persistence;
using LegalAssistant.Domain.Models;

namespace LegalAssistant.Api.Services;

public sealed class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documents;
    private readonly IJobRepository _jobs;
    private readonly IUnitOfWork _uow;
    private readonly IMessagePublisher _publisher;
    private readonly IClock _clock;

    public DocumentService(
        IDocumentRepository documents,
        IJobRepository jobs,
        IUnitOfWork uow,
        IMessagePublisher publisher,
        IClock clock)
    {
        _documents = documents;
        _jobs = jobs;
        _uow = uow;
        _publisher = publisher;
        _clock = clock;
    }

    public async Task<Guid> CreateDocumentAsync(string title, string url, string content, object metadata, CancellationToken cancellationToken = default)
    {
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            Title = title,
            Url = url,
            Content = content,
            Metadata = JsonSerializer.Serialize(metadata)
        };

        await _documents.AddAsync(doc, cancellationToken);

        var job = new JobRecord
        {
            Id = Guid.NewGuid(),
            Type = "ingest",
            Status = JobStatus.Queued,
            Payload = JsonSerializer.Serialize(new { DocumentId = doc.Id, Url = url })
        };

        await _jobs.AddAsync(job, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        await _publisher.PublishAsync("ingest", job.Id.ToString(), job.Payload, cancellationToken);

        return job.Id;
    }

    public Task<Document?> GetDocumentAsync(Guid id, CancellationToken cancellationToken = default)
        => _documents.GetByIdWithChunksAsync(id, cancellationToken);

    public async Task<bool> UpdateDocumentAsync(Guid id, string title, string content, object metadata, CancellationToken cancellationToken = default)
    {
        var doc = await _documents.GetByIdAsync(id, cancellationToken);
        if (doc == null) return false;

        doc.Title = title ?? doc.Title;
        doc.Content = content ?? doc.Content;
        doc.Metadata = metadata != null ? JsonSerializer.Serialize(metadata) : doc.Metadata;
        doc.UpdatedAt = _clock.UtcNow;
        _documents.Update(doc);
        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteDocumentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var doc = await _documents.GetByIdAsync(id, cancellationToken);
        if (doc == null) return false;

        doc.IsDeleted = true;
        doc.UpdatedAt = _clock.UtcNow;
        _documents.Update(doc);
        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }
}
