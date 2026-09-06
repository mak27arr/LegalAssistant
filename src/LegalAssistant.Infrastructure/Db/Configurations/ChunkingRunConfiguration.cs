using LegalAssistant.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalAssistant.Infrastructure.Db.Configurations;

public sealed class ChunkingRunConfiguration : IEntityTypeConfiguration<ChunkingRun>
{
    public void Configure(EntityTypeBuilder<ChunkingRun> builder)
    {
        builder.ToTable("chunking_runs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DocumentId)
            .HasColumnName("document_id")
            .IsRequired();

        builder.Property(x => x.JobId)
            .HasColumnName("job_id");

        builder.Property(x => x.StrategyName)
            .HasColumnName("strategy_name")
            .IsRequired();

        builder.Property(x => x.StrategyVersion)
            .HasColumnName("strategy_version")
            .IsRequired();

        builder.Property(x => x.ParamsJson)
            .HasColumnName("params_json")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasColumnName("status")
            .HasDefaultValue(ChunkingRunStatus.InProgress)
            .IsRequired();
        builder.Property(x => x.TotalChunks).HasColumnName("total_chunks");
        builder.Property(x => x.CompletedChunks).HasColumnName("completed_chunks");
        builder.Property(x => x.FailedChunks).HasColumnName("failed_chunks");
        builder.Property(x => x.LastError).HasColumnName("last_error");
        builder.Property(x => x.EmbeddingCompletedAt).HasColumnName("embedding_completed_at");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasOne(x => x.Document)
            .WithMany(d => d.ChunkingRuns)
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.DocumentId, x.CreatedAt });
        builder.HasIndex(x => x.JobId)
            .IsUnique()
            .HasFilter("\"job_id\" IS NOT NULL");
    }
}
