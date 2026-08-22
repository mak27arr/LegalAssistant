using LegalAssistant.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalAssistant.Infrastructure.Db.Configurations;

public sealed class OutboxMessageRecordConfiguration : IEntityTypeConfiguration<OutboxMessageRecord>
{
    public void Configure(EntityTypeBuilder<OutboxMessageRecord> b)
    {
        b.ToTable("message_outbox");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.JobId).HasColumnName("job_id").IsRequired();
        b.Property(x => x.MessageType).HasColumnName("message_type").HasMaxLength(150).IsRequired();
        b.Property(x => x.RoutingKey).HasColumnName("routing_key").HasMaxLength(200).IsRequired();
        b.Property(x => x.Payload).HasColumnName("payload").IsRequired();
        b.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasColumnName("status").HasMaxLength(50);
        b.Property(x => x.Attempts).HasColumnName("attempts");
        b.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1).IsConcurrencyToken();
        b.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        b.Property(x => x.LastError).HasColumnName("last_error");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        b.Property(x => x.PublishedAt).HasColumnName("published_at");

        b.HasIndex(x => x.JobId).IsUnique();
        b.HasIndex(x => new { x.Status, x.NextAttemptAt, x.CreatedAt });
    }
}
