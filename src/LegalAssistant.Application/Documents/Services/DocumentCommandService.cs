using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Application.Common;
using LegalAssistant.Application.Documents.Models;
using LegalAssistant.Application.Jobs;
using LegalAssistant.Application.Persistence;
using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Documents.Services;

public sealed class DocumentCommandService : IDocumentCommandService
{
    private readonly IDocumentRepository _documents;
    private readonly IJobRepository _jobs;
    private readonly IUnitOfWork _uow;
    private readonly IDocumentIngestJobPublisher _publisher;
    private readonly IClock _clock;

    public DocumentCommandService(
        IDocumentRepository documents,
        IJobRepository jobs,
        IUnitOfWork uow,
        IDocumentIngestJobPublisher publisher,
        IClock clock)
    {
        _documents = documents;
        _jobs = jobs;
        _uow = uow;
        _publisher = publisher;
        _clock = clock;
    }

    public async Task<CreateDocumentResult> CreateAsync(CreateDocumentCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Title))
            throw new ArgumentException("Title is required", nameof(command));
        if (string.IsNullOrWhiteSpace(command.Url))
            throw new ArgumentException("Url is required", nameof(command));

        var now = _clock.UtcNow;

        var doc = new Document
        {
            Id = Guid.NewGuid(),
            Title = command.Title,
            Url = command.Url,
            Content = command.Content,
            Metadata = JsonSerializer.Serialize(command.Metadata),
            CreatedAt = now,
            UpdatedAt = now
        };

        await _documents.AddAsync(doc, cancellationToken);

        var payload = JsonSerializer.Serialize(new { DocumentId = doc.Id, Url = doc.Url });
        var job = new JobRecord
        {
            Id = Guid.NewGuid(),
            Type = "ingest",
            Status = JobStatus.Queued,
            Payload = payload,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _jobs.AddAsync(job, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        await _publisher.PublishAsync(job.Id, job.Payload, cancellationToken);

        return new CreateDocumentResult(doc.Id, job.Id);
    }

    public async Task<bool> UpdateAsync(UpdateDocumentCommand command, CancellationToken cancellationToken = default)
    {
        var doc = await _documents.GetByIdAsync(command.DocumentId, cancellationToken);
        if (doc == null) return false;

        doc.Title = command.Title ?? doc.Title;
        doc.Content = command.Content ?? doc.Content;
        doc.Metadata = command.Metadata != null ? JsonSerializer.Serialize(command.Metadata) : doc.Metadata;
        
        var now = _clock.UtcNow;
        doc.UpdatedAt = now;
        _documents.Update(doc);

        var payload = JsonSerializer.Serialize(new { DocumentId = doc.Id, Url = doc.Url });
        var job = new JobRecord
        {
            Id = Guid.NewGuid(),
            Type = "ingest",
            Status = JobStatus.Queued,
            Payload = payload,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _jobs.AddAsync(job, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        await _publisher.PublishAsync(job.Id, job.Payload, cancellationToken);

        return true;
    }

    public async Task<bool> DeleteAsync(DeleteDocumentCommand command, CancellationToken cancellationToken = default)
    {
        var doc = await _documents.GetByIdAsync(command.DocumentId, cancellationToken);
        if (doc == null) return false;

        doc.IsDeleted = true;
        doc.UpdatedAt = _clock.UtcNow;
        _documents.Update(doc);
        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }
}
