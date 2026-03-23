namespace LegalAssistant.Application.Documents.Models;

public sealed record CreateDocumentCommand(string Title, string Url, string Content, object Metadata);
