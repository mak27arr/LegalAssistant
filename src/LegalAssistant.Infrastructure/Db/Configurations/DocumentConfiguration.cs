using LegalAssistant.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalAssistant.Infrastructure.Db.Configurations;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> b)
    {
        b.ToTable("documents");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");

        b.Property(x => x.Title).HasMaxLength(1000).HasColumnName("title");
        b.Property(x => x.Url).HasMaxLength(2000).HasColumnName("url");
        b.Property(x => x.Content).HasColumnName("content");
        b.Property(x => x.Metadata).HasColumnName("metadata");
        b.Property(x => x.ActiveChunkingRunId).HasColumnName("active_chunking_run_id");

        b.Property(x => x.Version).HasDefaultValue(1).HasColumnName("version");
        b.Property(x => x.IsDeleted).HasDefaultValue(false).HasColumnName("is_deleted");

        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
    }
}
