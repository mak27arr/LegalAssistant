using LegalAssistant.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalAssistant.Infrastructure.Db.Configurations;

public sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> b)
    {
        b.ToTable("document_chunks");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.DocumentId).HasColumnName("document_id");
        b.Property(x => x.ChunkIndex).HasColumnName("chunk_index");

        b.Property(x => x.Text).HasColumnName("text");
        b.Property(x => x.CharRange).HasMaxLength(100).HasColumnName("char_range");
        b.Property(x => x.SourceUrl).HasMaxLength(2000).HasColumnName("source_url");

        b.Property(x => x.Embedding)
            .HasColumnName("embedding")
            .HasColumnType("vector(768)");

        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");

        b.HasOne(x => x.Document)
            .WithMany(d => d.Chunks)
            .HasForeignKey(x => x.DocumentId);
    }
}
