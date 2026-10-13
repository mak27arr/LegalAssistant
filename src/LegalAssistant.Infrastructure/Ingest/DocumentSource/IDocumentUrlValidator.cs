using System.Threading;
using System.Threading.Tasks;

namespace LegalAssistant.Infrastructure.Documents;

public interface IDocumentUrlValidator
{
    Task ValidateAsync(string url, CancellationToken cancellationToken = default);
}
