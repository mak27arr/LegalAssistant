using LegalAssistant.Application.Chunking.Models;
using LegalAssistant.Application.Chunking.Services;
using LegalAssistant.Application.Common;
using LegalAssistant.Application.Documents;
using LegalAssistant.Application.Embeddings;
using LegalAssistant.Application.Jobs.Models;
using LegalAssistant.Application.Jobs;
using LegalAssistant.Application.Jobs.Services;
using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Domain.Chunking;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Chunks;
using LegalAssistant.Infrastructure.Chunking;
using LegalAssistant.Infrastructure.Db;
using LegalAssistant.Infrastructure.Documents;
using LegalAssistant.Infrastructure.Embeddings;
using LegalAssistant.Infrastructure.Jobs;
using LegalAssistant.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LegalAssistant.BackendTests.Jobs;

public sealed class IngestJobProcessorIdempotencyTests
{
    [Fact]
    public async Task RedeliveryAfterChunkPersistence_ShouldReuseRunChunksAndOutboxMessages()
    {
        var clock = new TestClock(new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc));
        await using var db = CreateDbContext();
        var document = new Document
        {
            Id = Guid.NewGuid(),
            Title = "Test document",
            Url = "https://example.test/document",
            Content = "abcdefghij",
            Metadata = "{}",
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow
        };
        var job = new JobRecord
        {
            Id = Guid.NewGuid(),
            Type = "ingest",
            Status = JobStatus.Queued,
            Payload = $"{{\"DocumentId\":\"{document.Id}\"}}",
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow
        };
        db.Documents.Add(document);
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var selector = new Mock<IChunkingStrategySelector>();
        selector.Setup(x => x.Select(It.IsAny<ChunkingRunContext>()))
            .Returns((ChunkingRunContext _) =>
                (new ChunkingRunDescriptor("test", "test", "1", "{}"), new TestPolicy()));
        var status = new Mock<IEmbeddingStatusService>();
        status.SetupSequence(x => x.FinalizeRunAsync(It.IsAny<Guid>(), job.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated crash after persistence"))
            .ReturnsAsync(new EmbeddingStatusUpdateResult(
                true,
                false,
                false,
                2,
                0,
                0,
                JobStatus.EmbeddingInProgress));

        var processor = CreateProcessor(db, clock, selector.Object, status.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync(job.Id));
        await processor.ProcessAsync(job.Id);

        Assert.Equal(1, await db.ChunkingRuns.CountAsync(x => x.JobId == job.Id));
        Assert.Equal(2, await db.DocumentChunks.CountAsync(x => x.ChunkingRunId != null));
        Assert.Equal(2, await db.OutboxMessages.CountAsync(x => x.MessageType == "embedding.requested"));
    }

    [Fact]
    public async Task DifferentJobsForSameDocument_ShouldCreateSeparateRuns()
    {
        var clock = new TestClock(new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc));
        await using var db = CreateDbContext();
        var document = new Document
        {
            Id = Guid.NewGuid(),
            Title = "Test document",
            Url = "https://example.test/document",
            Content = "abcdefghij",
            Metadata = "{}",
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow
        };
        var firstJob = CreateJob(document.Id, clock.UtcNow);
        var secondJob = CreateJob(document.Id, clock.UtcNow);
        db.Documents.Add(document);
        db.Jobs.AddRange(firstJob, secondJob);
        await db.SaveChangesAsync();

        var selector = new Mock<IChunkingStrategySelector>();
        selector.Setup(x => x.Select(It.IsAny<ChunkingRunContext>()))
            .Returns((ChunkingRunContext _) =>
                (new ChunkingRunDescriptor("test", "test", "1", "{}"), new TestPolicy()));
        var status = new Mock<IEmbeddingStatusService>();
        status.Setup(x => x.FinalizeRunAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingStatusUpdateResult(
                true,
                false,
                false,
                2,
                0,
                0,
                JobStatus.EmbeddingInProgress));

        var processor = CreateProcessor(db, clock, selector.Object, status.Object);
        await processor.ProcessAsync(firstJob.Id);
        await processor.ProcessAsync(secondJob.Id);

        Assert.Equal(2, await db.ChunkingRuns.CountAsync());
        Assert.Equal(4, await db.DocumentChunks.CountAsync());
        Assert.Equal(4, await db.OutboxMessages.CountAsync(x => x.MessageType == "embedding.requested"));
    }

    private static IngestJobProcessor CreateProcessor(
        LegalAssistantDbContext db,
        IClock clock,
        IChunkingStrategySelector selector,
        IEmbeddingStatusService statuses)
    {
        var options = new IngestJobProcessingOptions
        {
            MaxAttempts = 3,
            InitialDelaySeconds = 0,
            MaxDelaySeconds = 0,
            BackoffMultiplier = 1,
            LeaseDurationSeconds = 60
        };
        var documents = new EfDocumentRepository(db);
        var outbox = new EfMessageOutboxWriter(db);
        return new IngestJobProcessor(
            documents,
            new EfDocumentChunkRepository(db),
            new EfJobRepository(db, clock),
            new EfUnitOfWork(db),
            new EmbeddingRequestOutboxWriter(db, outbox, clock),
            statuses,
            new ChunkingRunService(selector, clock),
            new EfChunkingRunRepository(db),
            new Mock<IDocumentContentFetcher>().Object,
            clock,
            options,
            NullLogger<IngestJobProcessor>.Instance);
    }

    private static LegalAssistantDbContext CreateDbContext()
        => new TestDbContext(new DbContextOptionsBuilder<LegalAssistantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static JobRecord CreateJob(Guid documentId, DateTime now)
        => new()
        {
            Id = Guid.NewGuid(),
            Type = "ingest",
            Status = JobStatus.Queued,
            Payload = $"{{\"DocumentId\":\"{documentId}\"}}",
            CreatedAt = now,
            UpdatedAt = now
        };

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

    private sealed class TestPolicy : IChunkingPolicy
    {
        public IEnumerable<ChunkRange> GetRanges(string text)
            => [new ChunkRange(0, 5), new ChunkRange(5, 5)];
    }

    private sealed class TestClock : IClock
    {
        public TestClock(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; }
    }
}
