using LegalAssistant.Application.Rag.Models;
using LegalAssistant.Application.Rag.Services;
using Microsoft.Extensions.Configuration;

namespace LegalAssistant.Infrastructure.Rag;

public sealed class ConfigurationRagQueryPolicyProvider : IRagQueryPolicyProvider
{
    private readonly IConfiguration _configuration;

    public ConfigurationRagQueryPolicyProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<RagQueryPolicy> GetAsync(string? userId = null, CancellationToken cancellationToken = default)
    {
        var policy = new RagQueryPolicy(
            _configuration.GetValue<int>("Rag:DefaultTopK"),
            _configuration.GetValue<int>("Rag:MaxTopK"),
            _configuration.GetValue<int>("Rag:PromptTokenBudget"),
            _configuration.GetValue<int>("Rag:ApproxCharsPerToken"));

        return Task.FromResult(policy);
    }
}
