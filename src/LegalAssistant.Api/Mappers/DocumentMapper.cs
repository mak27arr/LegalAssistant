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
            document.ChunkCount,
            document.ProcessingStatus);

    public static DocumentListPageDto Map(DocumentListPageResult page)
        => new(
            page.Items.Select(Map).ToList(),
            page.Page,
            page.PageSize,
            page.TotalItems,
            page.TotalPages,
            page.HasNextPage,
            page.HasPreviousPage);

    public static DocumentDetailsDto Map(DocumentDetailsResult document)
        => new(
            document.Id,
            document.Title,
            document.Url,
            document.Version,
            document.CreatedAt,
            document.UpdatedAt,
            document.ChunkCount,
            document.ProcessingStatus,
            document.EmbeddingCount,
            document.CompletedEmbeddingCount,
            document.FailedEmbeddingCount);
}
