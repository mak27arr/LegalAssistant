using LegalAssistant.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalAssistant.Infrastructure.Db.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Email).HasMaxLength(320).HasColumnName("email").IsRequired();
        b.Property(x => x.FullName).HasMaxLength(500).HasColumnName("full_name").IsRequired();
        b.Property(x => x.GoogleSubjectId).HasMaxLength(255).HasColumnName("google_subject_id").IsRequired();
        b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        b.Property(x => x.LastLoginAt).HasColumnName("last_login_at");

        b.HasIndex(x => x.Email).IsUnique();
        b.HasIndex(x => x.GoogleSubjectId).IsUnique();
    }
}
