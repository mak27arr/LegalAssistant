using System;
using Microsoft.EntityFrameworkCore;
using LegalAssistant.Domain.Models;
using Npgsql;

namespace LegalAssistant.Infrastructure.Db
{
    public class LegalAssistantDbContext : DbContext
    {
        public LegalAssistantDbContext(DbContextOptions<LegalAssistantDbContext> options) : base(options) { }

        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentChunk> DocumentChunks { get; set; }
        public DbSet<ChunkingRun> ChunkingRuns { get; set; }
        public DbSet<JobRecord> Jobs { get; set; }
        public DbSet<RagPromptTemplate> RagPromptTemplates { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("vector");

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(LegalAssistantDbContext).Assembly);

            base.OnModelCreating(modelBuilder);
        }
    }
}
