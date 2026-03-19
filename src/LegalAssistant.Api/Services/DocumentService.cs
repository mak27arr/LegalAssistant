using System;
using System.Text.Json;
using System.Threading.Tasks;
using LegalAssistant.Application.Documents;
using LegalAssistant.Application.Jobs;
using LegalAssistant.Application.Persistence;
using LegalAssistant.Domain.Models;
using LegalAssistant.Api.Messaging;

namespace LegalAssistant.Api.Services
{
    public interface IDocumentService
    {
        Task<Guid> CreateDocumentAsync(string title, string url, string content, object metadata);
        Task<Document> GetDocumentAsync(Guid id);
        Task<bool> UpdateDocumentAsync(Guid id, string title, string content, object metadata);
        Task<bool> DeleteDocumentAsync(Guid id);
    }

    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _documents;
        private readonly IJobRepository _jobs;
        private readonly IUnitOfWork _uow;
        private readonly IMessagePublisher _publisher;

        public DocumentService(IDocumentRepository documents, IJobRepository jobs, IUnitOfWork uow, IMessagePublisher publisher)
        {
            _documents = documents;
            _jobs = jobs;
            _uow = uow;
            _publisher = publisher;
        }

        public async Task<Guid> CreateDocumentAsync(string title, string url, string content, object metadata)
        {
            var doc = new Document
            {
                Id = Guid.NewGuid(),
                Title = title,
                Url = url,
                Content = content,
                Metadata = JsonSerializer.Serialize(metadata)
            };

            await _documents.AddAsync(doc);

            var job = new JobRecord
            {
                Id = Guid.NewGuid(),
                Type = "ingest",
                Status = JobStatus.Queued,
                Payload = JsonSerializer.Serialize(new { DocumentId = doc.Id, Url = url })
            };

            await _jobs.AddAsync(job);
            await _uow.SaveChangesAsync();

            await _publisher.PublishAsync("ingest", job.Id.ToString(), job.Payload);

            return job.Id;
        }

        public async Task<Document> GetDocumentAsync(Guid id)
        {
            return await _documents.GetByIdWithChunksAsync(id);
        }

        public async Task<bool> UpdateDocumentAsync(Guid id, string title, string content, object metadata)
        {
            var doc = await _documents.GetByIdAsync(id);
            if (doc == null) return false;
            doc.Title = title ?? doc.Title;
            doc.Content = content ?? doc.Content;
            doc.Metadata = metadata != null ? JsonSerializer.Serialize(metadata) : doc.Metadata;
            doc.UpdatedAt = DateTime.UtcNow;
            _documents.Update(doc);
            await _uow.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteDocumentAsync(Guid id)
        {
            var doc = await _documents.GetByIdAsync(id);
            if (doc == null) return false;
            doc.IsDeleted = true;
            doc.UpdatedAt = DateTime.UtcNow;
            _documents.Update(doc);
            await _uow.SaveChangesAsync();
            return true;
        }
    }
}
