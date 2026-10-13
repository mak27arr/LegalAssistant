using LegalAssistant.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalAssistant.Infrastructure.Db.Configurations;

public sealed class DataProtectionKeyRecordConfiguration : IEntityTypeConfiguration<DataProtectionKeyRecord>
{
    public void Configure(EntityTypeBuilder<DataProtectionKeyRecord> b)
    {
        b.ToTable("data_protection_keys");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.FriendlyName).HasMaxLength(200).HasColumnName("friendly_name").IsRequired();
        b.Property(x => x.Xml).HasColumnName("xml").IsRequired();

        b.HasIndex(x => x.FriendlyName).IsUnique();
    }
}
