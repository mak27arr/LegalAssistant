using System;
using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Application.Common;
using LegalAssistant.Application.Chunking.Models;
using LegalAssistant.Domain.Chunking;
using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Chunking.Services;

public sealed class ChunkingRunService : IChunkingRunService
{
    private readonly IChunkingStrategySelector _selector;
    private readonly IClock _clock;

    public ChunkingRunService(
        IChunkingStrategySelector selector,
        IClock clock)
    {
        _selector = selector;
        _clock = clock;
    }

    public Task<(ChunkingRun Run, IChunkingPolicy Policy)> CreateAsync(ChunkingRunContext context, CancellationToken cancellationToken = default)
    {
        var (descriptor, policy) = _selector.Select(context);

        var now = _clock.UtcNow;
        var run = new ChunkingRun
        {
            Id = Guid.NewGuid(),
            DocumentId = context.DocumentId,
            StrategyName = descriptor.StrategyName,
            StrategyVersion = descriptor.StrategyVersion,
            ParamsJson = descriptor.ParamsJson,
            Status = ChunkingRunStatus.InProgress,
            CreatedAt = now,
            UpdatedAt = now
        };

        return Task.FromResult((run, policy));
    }
}
