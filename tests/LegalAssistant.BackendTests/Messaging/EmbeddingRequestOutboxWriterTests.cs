using LegalAssistant.Application.Common;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using LegalAssistant.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.BackendTests.Messaging;

public sealed class EmbeddingRequestOutboxWriterTests
{
    [Fact]
    public async Task EnqueueEmbedding_ShouldDeduplicateByChunkId()
    {
        await using var db = CreateDbContext();
        var writer = CreateWriter(db);
        var chunkId = Guid.NewGuid();

        await writer.EnqueueEmbeddingAsync(chunkId, "same text", Guid.NewGuid(), Guid.NewGuid());
        await writer.EnqueueEmbeddingAsync(chunkId, "same text", Guid.NewGuid(), Guid.NewGuid());
        await db.SaveChangesAsync();

        var messages = await db.OutboxMessages.ToListAsync();
        Assert.Single(messages);
        Assert.Equal(chunkId.ToString("N"), messages[0].DeduplicationKey);
    }

    [Fact]
    public async Task EnqueueEmbedding_ShouldAllowMultipleChunksFromOneJob()
    {
        await using var db = CreateDbContext();
        var writer = CreateWriter(db);
        var jobId = Guid.NewGuid();

        await writer.EnqueueEmbeddingAsync(Guid.NewGuid(), "first", jobId, Guid.NewGuid());
        await writer.EnqueueEmbeddingAsync(Guid.NewGuid(), "second", jobId, Guid.NewGuid());
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.OutboxMessages.CountAsync());
    }

    [Fact]
    public void Model_ShouldDeclareDatabaseUniquenessBoundaries()
    {
        using var db = CreateDbContext();

        var chunkIndex = db.Model.FindEntityType(typeof(DocumentChunk))!
            .GetIndexes()
            .Single(x => x.Properties.Select(p => p.Name).SequenceEqual(["ChunkingRunId", "ChunkIndex"]));
        var runIndex = db.Model.FindEntityType(typeof(ChunkingRun))!
            .GetIndexes()
            .Single(x => x.Properties.Select(p => p.Name).SequenceEqual(["JobId"]));
        var outboxIndex = db.Model.FindEntityType(typeof(OutboxMessageRecord))!
            .GetIndexes()
            .Single(x => x.Properties.Select(p => p.Name).SequenceEqual(["MessageType", "DeduplicationKey"]));

        Assert.True(chunkIndex.IsUnique);
        Assert.True(runIndex.IsUnique);
        Assert.True(outboxIndex.IsUnique);
        Assert.Contains("IS NOT NULL", chunkIndex.GetFilter());
        Assert.Contains("IS NOT NULL", runIndex.GetFilter());
        Assert.Contains("IS NOT NULL", outboxIndex.GetFilter());
    }

    private static EmbeddingRequestOutboxWriter CreateWriter(LegalAssistantDbContext db)
        => new(db, new EfMessageOutboxWriter(db), new TestClock());

    private static LegalAssistantDbContext CreateDbContext()
        => new TestDbContext(new DbContextOptionsBuilder<LegalAssistantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private sealed class TestDbContext : LegalAssistantDbContext
    {
        public TestDbContext(DbContextOptions<LegalAssistantDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<DocumentChunk>().Ignore(x => x.Embedding);
        }
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = new(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
    }
}
