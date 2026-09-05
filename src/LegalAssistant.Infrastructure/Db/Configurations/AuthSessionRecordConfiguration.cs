using LegalAssistant.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalAssistant.Infrastructure.Db.Configurations;

public sealed class AuthSessionRecordConfiguration : IEntityTypeConfiguration<AuthSessionRecord>
{
    public void Configure(EntityTypeBuilder<AuthSessionRecord> b)
    {
        b.ToTable("auth_sessions");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasMaxLength(256).HasColumnName("id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.Ticket).HasColumnName("ticket").IsRequired();
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        b.Property(x => x.LastRenewedAt).HasDefaultValueSql("now()").HasColumnName("last_renewed_at");
        b.Property(x => x.ExpiresAt).HasColumnName("expires_at");

        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.ExpiresAt);
        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
