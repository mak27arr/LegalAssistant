using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Application.Ask;
using LegalAssistant.Application.Ask.Models;
using LegalAssistant.Application.Rag.Models;
using LegalAssistant.Application.Rag.Services;

namespace LegalAssistant.Application.Rag;

public sealed class RagAnswerService : IRagAnswerService
{
    private readonly IAskService _ask;
    private readonly ILlmClient _llm;
    private readonly IRagPromptTemplateProvider _promptTemplate;
    private readonly IRagPromptBuilder _promptBuilder;
    private readonly IRagAnswerValidator _validator;

    public RagAnswerService(
        IAskService ask,
        ILlmClient llm,
        IRagPromptTemplateProvider promptTemplate,
        IRagPromptBuilder promptBuilder,
        IRagAnswerValidator validator)
    {
        _ask = ask;
        _llm = llm;
        _promptTemplate = promptTemplate;
        _promptBuilder = promptBuilder;
        _validator = validator;
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
            built.TopK,
            answer,
            built.Sources,
            built.Prompt,
            validation.IsValid,
            validation.CitationIds,
            validation.Issues);
    }

    public async Task<RagPromptResult> BuildPromptAsync(RagAnswerQuery query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.Question))
            throw new ArgumentException("Question is required", nameof(query));

        var topK = query.TopK <= 0 ? 5 : query.TopK;
        var ask = await _ask.AskAsync(new AskQuery(query.Question, topK), cancellationToken);

        var template = await _promptTemplate.GetAsync(cancellationToken);
        var prompt = _promptBuilder.Build(template, ask.Question, ask.Chunks);
        var sources = ask.Chunks
            .Select(c => new RagAnswerSource(c.ChunkId, c.DocumentId, c.ChunkIndex, c.Text, c.SourceUrl, c.Score))
            .ToList();

        return new RagPromptResult(query.Question, topK, sources, prompt);
    }

    private static string BuildRefusalMessage()
        => "I could not produce a grounded answer from the provided sources. Please narrow the question or provide more relevant documents.";
}
