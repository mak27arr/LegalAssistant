namespace LegalAssistant.Application.Documents.Models;

public sealed record UpdateDocumentCommand(Guid DocumentId, string Title, string Content, object Metadata);
