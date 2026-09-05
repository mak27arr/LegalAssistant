using LegalAssistant.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalAssistant.Infrastructure.Db.Configurations;

public sealed class AskJobRecordConfiguration : IEntityTypeConfiguration<AskJobRecord>
{
    public void Configure(EntityTypeBuilder<AskJobRecord> b)
    {
        b.ToTable("ask_jobs");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.OwnerUserId).HasColumnName("owner_user_id");
        b.Property(x => x.ActorScopeKey).HasMaxLength(256).HasColumnName("actor_scope_key").IsRequired();
        b.Property(x => x.IdempotencyKey).HasMaxLength(256).HasColumnName("idempotency_key").IsRequired();
        b.Property(x => x.Question).HasColumnName("question").IsRequired();
        b.Property(x => x.TopK).HasColumnName("top_k");
        b.Property(x => x.ConversationId).HasMaxLength(256).HasColumnName("conversation_id");
        b.Property(x => x.RequestHash).HasMaxLength(128).HasColumnName("request_hash").IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).HasColumnName("status");
        b.Property(x => x.ResultJson).HasColumnName("result_json");
        b.Property(x => x.Error).HasColumnName("error");
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");

        b.HasIndex(x => new { x.OwnerUserId, x.IdempotencyKey }).IsUnique();
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.HasOne(x => x.OwnerUser)
            .WithMany()
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
