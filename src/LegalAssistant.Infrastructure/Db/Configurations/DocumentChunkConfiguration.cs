using LegalAssistant.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

namespace LegalAssistant.Infrastructure.Db.Configurations;

public sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> b)
    {
        b.ToTable("document_chunks");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.DocumentId).HasColumnName("document_id");
        b.Property(x => x.ChunkingRunId)
            .HasColumnName("chunking_run_id")
            .IsRequired(false);
        b.Property(x => x.ChunkIndex).HasColumnName("chunk_index");

        b.Property(x => x.Text).HasColumnName("text").IsRequired();
        b.Property(x => x.CharRange).HasMaxLength(100).HasColumnName("char_range").IsRequired();
        b.Property(x => x.SourceUrl).HasMaxLength(2000).HasColumnName("source_url").IsRequired();

        var embeddingConverter =
            new ValueConverter<EmbeddingVector?, Vector?>(
                v => v == null ? null : new Vector(v.Values.ToArray()),
                v => v == null ? null : new EmbeddingVector(v.ToArray()));
        b.Property(x => x.Embedding)
            .HasColumnName("embedding")
            .HasColumnType("vector(768)")
            .HasConversion(embeddingConverter);

        b.Property(x => x.EmbeddingStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasColumnName("embedding_status")
            .HasDefaultValue(EmbeddingStatus.Pending)
            .IsRequired();
        b.Property(x => x.EmbeddingAttemptCount).HasColumnName("embedding_attempt_count");
        b.Property(x => x.EmbeddingLastError).HasColumnName("embedding_last_error");
        b.Property(x => x.EmbeddingStartedAt).HasColumnName("embedding_started_at");
        b.Property(x => x.EmbeddingCompletedAt).HasColumnName("embedding_completed_at");
        b.Property(x => x.EmbeddingFailedAt).HasColumnName("embedding_failed_at");
        b.Property(x => x.EmbeddingUpdatedAt).HasColumnName("embedding_updated_at");
        b.Property(x => x.JobId).HasColumnName("job_id");

        b.HasIndex(x => x.Embedding)
            .HasDatabaseName("ix_document_chunks_embedding_hnsw")
            .HasMethod("hnsw")
            .HasOperators("vector_l2_ops")
            .HasFilter("\"embedding\" IS NOT NULL");
        b.HasIndex(x => new { x.JobId, x.EmbeddingStatus });
        b.HasIndex(x => new { x.ChunkingRunId, x.ChunkIndex })
            .IsUnique()
            .HasFilter("\"chunking_run_id\" IS NOT NULL");

        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");

        b.HasOne(x => x.Document)
            .WithMany(d => d.Chunks)
            .HasForeignKey(x => x.DocumentId);

        b.HasOne<ChunkingRun>()
            .WithMany()
            .HasForeignKey(x => x.ChunkingRunId);
    }
}
