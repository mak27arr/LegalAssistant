using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalAssistant.Infrastructure.Db.Configurations;

public static class DocumentChunkVectorSearchRowConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        var b = modelBuilder.Entity<DocumentChunkVectorSearchRow>();
        b.HasNoKey();
        b.ToView("document_chunks");

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.DocumentId).HasColumnName("document_id");
        b.Property(x => x.ChunkIndex).HasColumnName("chunk_index");
        b.Property(x => x.Text).HasColumnName("text");
        b.Property(x => x.CharRange).HasColumnName("char_range");
        b.Property(x => x.SourceUrl).HasColumnName("source_url");
        b.Property(x => x.Embedding)
            .HasColumnName("embedding")
            .HasColumnType("vector(768)");
        b.Property(x => x.EmbeddingStatus)
            .HasColumnName("embedding_status")
            .HasConversion<string>()
            .HasMaxLength(30);
        b.Property(x => x.EmbeddingAttemptCount).HasColumnName("embedding_attempt_count");
        b.Property(x => x.EmbeddingLastError).HasColumnName("embedding_last_error");
        b.Property(x => x.EmbeddingStartedAt).HasColumnName("embedding_started_at");
        b.Property(x => x.EmbeddingCompletedAt).HasColumnName("embedding_completed_at");
        b.Property(x => x.EmbeddingFailedAt).HasColumnName("embedding_failed_at");
        b.Property(x => x.EmbeddingUpdatedAt).HasColumnName("embedding_updated_at");
        b.Property(x => x.JobId).HasColumnName("job_id");
        b.Property(x => x.ChunkingRunId).HasColumnName("chunking_run_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
    }
}
