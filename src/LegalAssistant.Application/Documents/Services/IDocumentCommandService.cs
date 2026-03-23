using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Application.Documents.Models;

namespace LegalAssistant.Application.Documents.Services;

public partial interface IDocumentCommandService
{
    Task<CreateDocumentResult> CreateAsync(CreateDocumentCommand command, CancellationToken cancellationToken = default);
}
