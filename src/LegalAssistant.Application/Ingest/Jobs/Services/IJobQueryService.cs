using System;
using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Application.Jobs.Models;

namespace LegalAssistant.Application.Jobs.Services;

public interface IJobQueryService
{
    Task<JobDto?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default);
}
