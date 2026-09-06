using System;
using Microsoft.EntityFrameworkCore;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db.Configurations;
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
        public DbSet<AskJobRecord> AskJobs { get; set; }
        public DbSet<AskJobEventRecord> AskJobEvents { get; set; }
        public DbSet<OutboxMessageRecord> OutboxMessages { get; set; }
        public DbSet<RagPromptTemplate> RagPromptTemplates { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<AuthSessionRecord> AuthSessions { get; set; }
        public DbSet<DataProtectionKeyRecord> DataProtectionKeys { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("vector");

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(LegalAssistantDbContext).Assembly);

            // Pgvector.Vector is intentionally kept out of the InMemory model used
            // by development/tests. The read model is only needed by PostgreSQL
            // vector search and is configured explicitly to avoid convention-based
            // discovery by other providers.
            if (!string.Equals(Database.ProviderName, "Microsoft.EntityFrameworkCore.InMemory", StringComparison.Ordinal))
                DocumentChunkVectorSearchRowConfiguration.Configure(modelBuilder);

            base.OnModelCreating(modelBuilder);
        }
    }
}
