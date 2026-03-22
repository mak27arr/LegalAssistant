using System.Threading;
using System.Threading.Tasks;

using LegalAssistant.Application.Rag.Models;

namespace LegalAssistant.Application.Rag;

public interface IRagPromptTemplateProvider
{
    Task<RagPromptTemplateDto> GetAsync(CancellationToken cancellationToken = default);
}
