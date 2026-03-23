using System.Collections.Generic;
using LegalAssistant.Application.Ask.Models;
using LegalAssistant.Application.Rag.Models;

namespace LegalAssistant.Application.Rag.Services;

public interface IRagPromptBuilder
{
    string Build(RagPromptTemplateDto template, string question, IReadOnlyList<AskChunkResult> chunks);
}
