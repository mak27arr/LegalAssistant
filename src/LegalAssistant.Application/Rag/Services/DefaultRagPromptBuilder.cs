using System.Collections.Generic;
using System.Text;
using LegalAssistant.Application.Ask.Models;

namespace LegalAssistant.Application.Rag.Services;

public sealed class DefaultRagPromptBuilder : IRagPromptBuilder
{
    public string Build(string systemHeader, string instructionsFooter, string question, IReadOnlyList<AskChunkResult> chunks)
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
