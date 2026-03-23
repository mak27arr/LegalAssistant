using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Application.Documents.Models;

namespace LegalAssistant.Application.Documents.Services;

public partial interface IDocumentCommandService
{
    Task<bool> UpdateAsync(UpdateDocumentCommand command, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(DeleteDocumentCommand command, CancellationToken cancellationToken = default);
}
