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

        b.Property(x => x.Type).HasMaxLength(100).HasColumnName("type");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).HasColumnName("status");
        b.Property(x => x.Payload).HasColumnName("payload");
        b.Property(x => x.Result).HasColumnName("result");

        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
    }
}
