using System.Threading;
using System.Threading.Tasks;

namespace LegalAssistant.Application.Rag;

public interface ILlmClient
{
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
}
