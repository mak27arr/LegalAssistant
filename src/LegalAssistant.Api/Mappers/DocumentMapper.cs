using LegalAssistant.Api.Dtos.Documents;
using LegalAssistant.Application.Documents.Models;

namespace LegalAssistant.Api.Mappers;

public static class DocumentMapper
{
    public static DocumentListItemDto Map(DocumentListItemResult document)
        => new(
            document.Id,
            document.Title,
            document.Url,
            document.Version,
            document.CreatedAt,
            document.UpdatedAt,
            document.ChunkCount);

    public static DocumentDetailsDto Map(DocumentDetailsResult document)
        => new(
            document.Id,
            document.Title,
            document.Url,
            document.Version,
            document.CreatedAt,
            document.UpdatedAt,
            document.ChunkCount);
}
