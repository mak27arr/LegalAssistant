using System;
using System.Threading;
using System.Threading.Tasks;

namespace LegalAssistant.Application.Jobs.Services;

public interface IIngestJobProcessor
{
    Task ProcessAsync(Guid jobId, CancellationToken cancellationToken = default);
}
