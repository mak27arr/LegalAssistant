using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Application.Ask;
using LegalAssistant.Application.Ask.Models;
using LegalAssistant.Application.Rag.Models;

namespace LegalAssistant.Application.Rag;

public sealed class RagAnswerService : IRagAnswerService
{
    private readonly IAskService _ask;
    private readonly ILlmClient _llm;
    private readonly IRagPromptTemplateProvider _promptTemplate;

    public RagAnswerService(IAskService ask, ILlmClient llm, IRagPromptTemplateProvider promptTemplate)
    {
        _ask = ask;
        _llm = llm;
        _promptTemplate = promptTemplate;
    }

    public async Task<RagAnswerResult> AnswerAsync(RagAnswerQuery query, CancellationToken cancellationToken = default)
    {
        var built = await BuildPromptAsync(query, cancellationToken);
        var answer = await _llm.GenerateAsync(built.Prompt, cancellationToken);
        return new RagAnswerResult(built.Question, built.TopK, answer, built.Sources, built.Prompt);
    }

    public async Task<RagPromptResult> BuildPromptAsync(RagAnswerQuery query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.Question))
            throw new ArgumentException("Question is required", nameof(query));

        var topK = query.TopK <= 0 ? 5 : query.TopK;
        var ask = await _ask.AskAsync(new AskQuery(query.Question, topK), cancellationToken);

        var template = await _promptTemplate.GetAsync(cancellationToken);
        var prompt = BuildPrompt(template.SystemHeader, template.InstructionsFooter, ask.Question, ask.Chunks);
        var sources = ask.Chunks
            .Select(c => new RagAnswerSource(c.ChunkId, c.DocumentId, c.ChunkIndex, c.Text, c.SourceUrl, c.Score))
            .ToList();

        return new RagPromptResult(query.Question, topK, sources, prompt);
    }

    private static string BuildPrompt(string systemHeader, string instructionsFooter, string question, IReadOnlyList<AskChunkResult> chunks)
    {
        var sb = new StringBuilder();
        sb.AppendLine(systemHeader);
        sb.AppendLine();
        sb.AppendLine("Питання:");
        sb.AppendLine(question);
        sb.AppendLine();
        sb.AppendLine("Джерела (витяги):");
        sb.AppendLine();

        var i = 1;
        foreach (var c in chunks)
        {
            sb.AppendLine($"[{i}] doc={c.DocumentId} chunk={c.ChunkIndex} score={c.Score:0.####} url={c.SourceUrl}");
            sb.AppendLine(c.Text);
            sb.AppendLine();
            i++;
        }

        sb.AppendLine("Інструкції:");
        sb.AppendLine(instructionsFooter);

        return sb.ToString();
    }
}
