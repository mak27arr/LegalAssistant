using LegalAssistant.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalAssistant.Infrastructure.Db.Configurations;

public sealed class RagPromptTemplateConfiguration : IEntityTypeConfiguration<RagPromptTemplate>
{
    public void Configure(EntityTypeBuilder<RagPromptTemplate> builder)
    {
        builder.ToTable("rag_prompt_templates");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SystemHeader)
            .HasColumnName("system_header")
            .IsRequired();

        builder.Property(x => x.InstructionsFooter)
            .HasColumnName("instructions_footer")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
    }
}
