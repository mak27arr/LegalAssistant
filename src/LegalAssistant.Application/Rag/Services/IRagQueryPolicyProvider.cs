using LegalAssistant.Application.Rag.Models;

namespace LegalAssistant.Application.Rag.Services;

public interface IRagQueryPolicyProvider
{
    Task<RagQueryPolicy> GetAsync(string? userId = null, CancellationToken cancellationToken = default);
}
