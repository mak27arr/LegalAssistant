using System;
using System.Threading;
using System.Threading.Tasks;

namespace LegalAssistant.Application.Documents.Services;

public interface IDocumentIngestJobPublisher
{
    Task PublishAsync(Guid jobId, string payload, CancellationToken cancellationToken = default);
}
