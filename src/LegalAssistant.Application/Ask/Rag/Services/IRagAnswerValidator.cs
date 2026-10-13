using LegalAssistant.Application.Rag.Models;

namespace LegalAssistant.Application.Rag.Services;

public interface IRagAnswerValidator
{
    RagAnswerValidationResult Validate(string question, string answer, IReadOnlyList<RagAnswerSource> sources);
}
