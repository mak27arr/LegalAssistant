using System;
using Microsoft.EntityFrameworkCore;
using LegalAssistant.Domain.Models;

namespace LegalAssistant.Infrastructure.Db
{
    public class LegalAssistantDbContext : DbContext
    {
        public LegalAssistantDbContext(DbContextOptions<LegalAssistantDbContext> options) : base(options) { }

        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentChunk> DocumentChunks { get; set; }
        public DbSet<JobRecord> Jobs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Document>(b =>
            {
                b.ToTable("documents");
                b.HasKey(x => x.Id);
                b.Property(x => x.Title).HasMaxLength(1000);
                b.Property(x => x.Url).HasMaxLength(2000);
                b.Property(x => x.Content);
                b.Property(x => x.Metadata);
                b.Property(x => x.Version).HasDefaultValue(1);
                b.Property(x => x.IsDeleted).HasDefaultValue(false);
                b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
                b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            });

            modelBuilder.Entity<DocumentChunk>(b =>
            {
                b.ToTable("document_chunks");
                b.HasKey(x => x.Id);
                b.Property(x => x.Text);
                b.Property(x => x.CharRange).HasMaxLength(100);
                b.Property(x => x.SourceUrl).HasMaxLength(2000);
                b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
                b.HasOne(x => x.Document).WithMany(d => d.Chunks).HasForeignKey(x => x.DocumentId);
            });

            modelBuilder.Entity<JobRecord>(b =>
            {
                b.ToTable("jobs");
                b.HasKey(x => x.Id);
                b.Property(x => x.Type).HasMaxLength(100);
                b.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
                b.Property(x => x.Payload);
                b.Property(x => x.Result);
                b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
                b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
