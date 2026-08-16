using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Application.Ask;
using LegalAssistant.Application.Ask.Models;
using LegalAssistant.Application.Rag.Models;
using LegalAssistant.Application.Rag.Services;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Application.Rag;

public sealed class RagAnswerService : IRagAnswerService
{
    private readonly IAskService _ask;
    private readonly ILlmClient _llm;
    private readonly IRagPromptTemplateProvider _promptTemplate;
    private readonly IRagPromptBuilder _promptBuilder;
    private readonly IRagQueryPolicyProvider _policyProvider;
    private readonly ILogger<RagAnswerService> _logger;
    private readonly IRagAnswerValidator _validator;

    public RagAnswerService(
        IAskService ask,
        ILlmClient llm,
        IRagPromptTemplateProvider promptTemplate,
        IRagPromptBuilder promptBuilder,
        IRagQueryPolicyProvider policyProvider,
        IRagAnswerValidator validator,
        ILogger<RagAnswerService> logger)
    {
        _ask = ask;
        _llm = llm;
        _promptTemplate = promptTemplate;
        _promptBuilder = promptBuilder;
        _policyProvider = policyProvider;
        _validator = validator;
        _logger = logger;
    }

    public async Task<RagAnswerResult> AnswerAsync(RagAnswerQuery query, CancellationToken cancellationToken = default)
    {
        var built = await BuildPromptAsync(query, cancellationToken);
        var answer = await _llm.GenerateAsync(built.Prompt, cancellationToken);
        var validation = _validator.Validate(answer, built.Sources);

        if (!validation.IsValid)
        {
            answer = BuildRefusalMessage();
        }

        return new RagAnswerResult(
            built.Question,
            built.RequestedTopK,
            built.TopK,
            built.UsedChunkCount,
            answer,
            built.Sources,
            built.Prompt,
            built.PromptTokenBudget,
            built.PromptTokenEstimate,
            built.WasTruncatedByBudget,
            validation.IsValid,
            validation.CitationIds,
            validation.Issues);
    }

    public async Task<RagPromptResult> BuildPromptAsync(RagAnswerQuery query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.Question))
            throw new ArgumentException("Question is required", nameof(query));

        var policy = await _policyProvider.GetAsync(cancellationToken: cancellationToken);
        var requestedTopK = query.TopK <= 0 ? policy.DefaultTopK : query.TopK;
        var effectiveTopK = Math.Min(requestedTopK, policy.MaxTopK);
        if (effectiveTopK != requestedTopK)
        {
            _logger.LogWarning(
                "RAG topK capped from {RequestedTopK} to {EffectiveTopK}. MaxTopK={MaxTopK}",
                requestedTopK,
                effectiveTopK,
                policy.MaxTopK);
        }

        var ask = await _ask.AskAsync(new AskQuery(query.Question, effectiveTopK), cancellationToken);

        var template = await _promptTemplate.GetAsync(cancellationToken);
        var prompt = _promptBuilder.Build(template, ask.Question, ask.Chunks, requestedTopK, effectiveTopK, policy);

        if (prompt.WasTruncatedByBudget)
        {
            _logger.LogWarning(
                "RAG prompt truncated by token budget. Budget={Budget} EstimatedTokens={EstimatedTokens} RequestedTopK={RequestedTopK} EffectiveTopK={EffectiveTopK} IncludedChunks={IncludedChunks} RetrievedChunks={RetrievedChunks}",
                prompt.PromptTokenBudget,
                prompt.PromptTokenEstimate,
                prompt.RequestedTopK,
                prompt.EffectiveTopK,
                prompt.UsedChunkCount,
                ask.Chunks.Count);
        }

        return new RagPromptResult(
            query.Question,
            prompt.RequestedTopK,
            prompt.EffectiveTopK,
            prompt.UsedChunkCount,
            prompt.Sources,
            prompt.Prompt,
            prompt.PromptTokenBudget,
            prompt.PromptTokenEstimate,
            prompt.WasTruncatedByBudget);
    }

    private static string BuildRefusalMessage()
        => "I could not produce a grounded answer from the provided sources. Please narrow the question or provide more relevant documents.";
}
