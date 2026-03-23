using System.Collections.Generic;
using LegalAssistant.Application.Ask.Models;

namespace LegalAssistant.Application.Rag.Services;

public interface IRagPromptBuilder
{
    string Build(string systemHeader, string instructionsFooter, string question, IReadOnlyList<AskChunkResult> chunks);
}
