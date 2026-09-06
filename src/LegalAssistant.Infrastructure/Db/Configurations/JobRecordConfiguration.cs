using LegalAssistant.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalAssistant.Infrastructure.Db.Configurations;

public sealed class JobRecordConfiguration : IEntityTypeConfiguration<JobRecord>
{
    public void Configure(EntityTypeBuilder<JobRecord> b)
    {
        b.ToTable("jobs");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");

        b.Property(x => x.Type).HasMaxLength(100).HasColumnName("type").IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).HasColumnName("status");
        b.Property(x => x.Payload).HasColumnName("payload").IsRequired();
        b.Property(x => x.Result).HasColumnName("result");
        b.Property(x => x.CorrelationId).HasMaxLength(100).HasColumnName("correlation_id");
        b.Property(x => x.StartedAt).HasColumnName("started_at");
        b.Property(x => x.AttemptCount).HasColumnName("attempt_count");
        b.Property(x => x.LastError).HasColumnName("last_error");
        b.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        b.Property(x => x.LeaseExpiresAt).HasColumnName("lease_expires_at");
        b.Property(x => x.LeaseId).HasColumnName("lease_id").IsConcurrencyToken();

        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
    }
}
