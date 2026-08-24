namespace LegalAssistant.Infrastructure.Documents;

public sealed record FetchedDocumentResponse(string FinalUri, string Body, string? MediaType)
{
    public bool IsHtml => !string.IsNullOrWhiteSpace(MediaType) &&
                          MediaType.Contains("html", StringComparison.OrdinalIgnoreCase);
}
