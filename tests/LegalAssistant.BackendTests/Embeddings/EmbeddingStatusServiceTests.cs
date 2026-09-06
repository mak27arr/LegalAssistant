using LegalAssistant.Application.Common;
using LegalAssistant.Application.Embeddings;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using LegalAssistant.Infrastructure.Db.Configurations;
using LegalAssistant.Infrastructure.Embeddings;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.BackendTests.Embeddings;

public sealed class EmbeddingStatusServiceTests
{
    [Fact]
    public async Task Completion_ShouldKeepParentInProgressUntilEveryChunkIsReady()
    {
        var clock = new TestClock(new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc));
        await using var db = CreateDbContext();
        var document = CreateDocument(clock.UtcNow);
        var job = CreateJob(document.Id, clock.UtcNow);
        var run = CreateRun(document.Id, clock.UtcNow);
        db.Documents.Add(document);
        db.Jobs.Add(job);
        db.ChunkingRuns.Add(run);
        var firstChunk = CreateChunk(document, run, job, 1);
        var secondChunk = CreateChunk(document, run, job, 2);
        db.DocumentChunks.AddRange(firstChunk, secondChunk);
        await db.SaveChangesAsync();

        var service = new EmbeddingStatusService(db, clock);
        Assert.True(await service.MarkInProgressAsync(firstChunk.Id, job.Id, run.Id));
        Assert.True(await service.MarkInProgressAsync(secondChunk.Id, job.Id, run.Id));

        var first = await service.MarkCompletedAsync(
            firstChunk.Id,
            Vector(0.1f),
            job.Id,
            run.Id);

        Assert.False(first.RunCompleted);
        Assert.Equal(JobStatus.EmbeddingInProgress, job.Status);

        var second = await service.MarkCompletedAsync(
            secondChunk.Id,
            Vector(0.3f),
            job.Id,
            run.Id);

        Assert.True(second.RunCompleted);
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal(ChunkingRunStatus.Completed, run.Status);
        Assert.Equal(2, run.CompletedChunks);
    }

    [Fact]
    public async Task FinalEmbeddingFailure_ShouldFailRunAndParentJob()
    {
        var clock = new TestClock(new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc));
        await using var db = CreateDbContext();
        var document = CreateDocument(clock.UtcNow);
        var job = CreateJob(document.Id, clock.UtcNow);
        var run = CreateRun(document.Id, clock.UtcNow);
        var chunk = CreateChunk(document, run, job, 1);
        db.Documents.Add(document);
        db.Jobs.Add(job);
        db.ChunkingRuns.Add(run);
        db.DocumentChunks.Add(chunk);
        await db.SaveChangesAsync();

        var service = new EmbeddingStatusService(db, clock);
        await service.MarkInProgressAsync(chunk.Id, job.Id, run.Id);
        var result = await service.RecordFailureAsync(
            chunk.Id,
            "Embedding generator: upstream unavailable",
            terminal: true,
            job.Id,
            run.Id);

        Assert.True(result.RunFailed);
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal(ChunkingRunStatus.Failed, run.Status);
        Assert.Equal(EmbeddingStatus.Failed, chunk.EmbeddingStatus);
        Assert.Equal("Embedding generator: upstream unavailable", job.LastError);
        Assert.Equal("Embedding generator: upstream unavailable", chunk.EmbeddingLastError);
    }

    [Fact]
    public async Task DuplicateCompletion_ShouldRemainIdempotent()
    {
        var clock = new TestClock(new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc));
        await using var db = CreateDbContext();
        var document = CreateDocument(clock.UtcNow);
        var job = CreateJob(document.Id, clock.UtcNow);
        var run = CreateRun(document.Id, clock.UtcNow);
        var chunk = CreateChunk(document, run, job, 1);
        db.Documents.Add(document);
        db.Jobs.Add(job);
        db.ChunkingRuns.Add(run);
        db.DocumentChunks.Add(chunk);
        await db.SaveChangesAsync();

        var service = new EmbeddingStatusService(db, clock);
        await service.MarkCompletedAsync(chunk.Id, Vector(0.1f), job.Id, run.Id);
        await service.MarkCompletedAsync(chunk.Id, Vector(0.1f), job.Id, run.Id);

        Assert.Equal(EmbeddingStatus.Completed, chunk.EmbeddingStatus);
        Assert.Equal(1, run.CompletedChunks);
        Assert.Equal(JobStatus.Completed, job.Status);
    }

    private static TestDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<LegalAssistantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static Document CreateDocument(DateTime now)
        => new()
        {
            Id = Guid.NewGuid(),
            Title = "Test document",
            Url = "https://example.test/document",
            Content = "Text",
            Metadata = "{}",
            CreatedAt = now,
            UpdatedAt = now
        };

    private static JobRecord CreateJob(Guid documentId, DateTime now)
        => new()
        {
            Id = Guid.NewGuid(),
            Type = "ingest",
            Status = JobStatus.InProgress,
            Payload = $"{{\"DocumentId\":\"{documentId}\"}}",
            CreatedAt = now,
            UpdatedAt = now
        };

    private static ChunkingRun CreateRun(Guid documentId, DateTime now)
        => new()
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            StrategyName = "test",
            StrategyVersion = "1",
            ParamsJson = "{}",
            CreatedAt = now,
            UpdatedAt = now
        };

    private static DocumentChunk CreateChunk(Document document, ChunkingRun run, JobRecord job, int index)
        => new()
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            ChunkingRunId = run.Id,
            JobId = job.Id,
            ChunkIndex = index,
            Text = $"Chunk {index}",
            CharRange = $"{index}-{index + 1}",
            SourceUrl = document.Url,
            CreatedAt = document.CreatedAt
        };

    private static float[] Vector(float value)
        => Enumerable.Repeat(value, EmbeddingStatusService.ExpectedEmbeddingDimensions).ToArray();

    private sealed class TestClock : IClock
    {
        public TestClock(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; }
    }

    private sealed class TestDbContext : LegalAssistantDbContext
    {
        public TestDbContext(DbContextOptions<LegalAssistantDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Ignore<AskJobRecord>();
            modelBuilder.Ignore<AskJobEventRecord>();
            modelBuilder.Ignore<OutboxMessageRecord>();
            modelBuilder.Ignore<RagPromptTemplate>();
            modelBuilder.Ignore<User>();
            modelBuilder.Ignore<Role>();
            modelBuilder.Ignore<UserRole>();
            modelBuilder.Ignore<RefreshToken>();
            modelBuilder.Ignore<AuthSessionRecord>();
            modelBuilder.Ignore<DataProtectionKeyRecord>();
            modelBuilder.ApplyConfiguration(new DocumentConfiguration());
            modelBuilder.ApplyConfiguration(new DocumentChunkConfiguration());
            modelBuilder.Entity<DocumentChunk>().Ignore(c => c.Embedding);
            modelBuilder.ApplyConfiguration(new ChunkingRunConfiguration());
            modelBuilder.ApplyConfiguration(new JobRecordConfiguration());
        }
    }
}
