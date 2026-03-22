namespace LegalAssistant.Api.Dtos.Documents;

public sealed record CreateDocumentRequest(string Title, string Url, string Content, object Metadata);

public sealed record UpdateDocumentRequest(string Title, string Content, object Metadata);
