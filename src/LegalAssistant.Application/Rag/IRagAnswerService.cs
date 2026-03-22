using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Application.Rag.Models;

namespace LegalAssistant.Application.Rag;

public interface IRagAnswerService
{
    Task<RagAnswerResult> AnswerAsync(RagAnswerQuery query, CancellationToken cancellationToken = default);

    Task<RagPromptResult> BuildPromptAsync(RagAnswerQuery query, CancellationToken cancellationToken = default);
}
