namespace LegalAssistant.Application.Jobs.Models;

public sealed record IngestJobPayload(string DocumentId, string? Url);
