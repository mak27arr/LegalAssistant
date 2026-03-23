using System.Collections.Generic;
using System.Text;
using LegalAssistant.Application.Ask.Models;
using LegalAssistant.Application.Rag.Models;

namespace LegalAssistant.Application.Rag.Services;

public sealed class DefaultRagPromptBuilder : IRagPromptBuilder
{
    public string Build(RagPromptTemplateDto template, string question, IReadOnlyList<AskChunkResult> chunks)
    {
        var sb = new StringBuilder();
        sb.AppendLine(template.SystemHeader);
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
        sb.AppendLine(template.InstructionsFooter);

        return sb.ToString();
    }
}
