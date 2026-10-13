using LegalAssistant.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalAssistant.Infrastructure.Db.Configurations;

public sealed class AskJobEventRecordConfiguration : IEntityTypeConfiguration<AskJobEventRecord>
{
    public void Configure(EntityTypeBuilder<AskJobEventRecord> b)
    {
        b.ToTable("ask_job_events");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        b.Property(x => x.JobId).HasColumnName("job_id").IsRequired();
        b.Property(x => x.ActorScopeKey).HasColumnName("actor_scope_key").HasMaxLength(256).IsRequired();
        b.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(256).IsRequired();
        b.Property(x => x.Question).HasColumnName("question").IsRequired();
        b.Property(x => x.TopK).HasColumnName("top_k");
        b.Property(x => x.ConversationId).HasColumnName("conversation_id").HasMaxLength(256);
        b.Property(x => x.Status).HasConversion<string>().HasColumnName("status").HasMaxLength(50).IsRequired();
        b.Property(x => x.ResultJson).HasColumnName("result_json");
        b.Property(x => x.Error).HasColumnName("error");
        b.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        b.HasIndex(x => new { x.JobId, x.Id });
        b.HasIndex(x => new { x.JobId, x.Status });
    }
}
