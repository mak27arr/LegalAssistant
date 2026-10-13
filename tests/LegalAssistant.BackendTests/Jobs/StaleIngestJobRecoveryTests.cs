using LegalAssistant.Application.Common;
using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Application.Jobs;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using LegalAssistant.Infrastructure.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.BackendTests.Jobs;

public sealed class StaleIngestJobRecoveryTests
{
    [Fact]
    public async Task DueRetry_ResetsPublishedOutboxOnce_AndLeavesMarkerlessLegacyRowAlone()
    {
        var now = new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);
        var clock = new TestClock(now);
        var databaseName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock>(clock);
        var dbOptions = new DbContextOptionsBuilder<LegalAssistantDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        services.AddScoped<LegalAssistantDbContext>(_ => new TestDbContext(dbOptions));
        using var provider = services.BuildServiceProvider();

        var dueJob = CreateJob(now, now);
        var legacyJob = CreateJob(now, null);
        var dueOutbox = CreateOutbox(dueJob, now, version: 4);
        var legacyOutbox = CreateOutbox(legacyJob, now, version: 9);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();
            db.Jobs.AddRange(dueJob, legacyJob);
            db.OutboxMessages.AddRange(dueOutbox, legacyOutbox);
            await db.SaveChangesAsync();
        }

        var options = new IngestJobProcessingOptions
        {
            MaxAttempts = 5,
            LeaseDurationSeconds = 60,
            RecoveryIntervalSeconds = 60,
            RecoveryBatchSize = 25
        };
        var recovery = new StaleIngestJobRecoveryHostedService(
            provider,
            clock,
            options,
            provider.GetRequiredService<ILogger<StaleIngestJobRecoveryHostedService>>());

        await recovery.StartAsync(CancellationToken.None);
        try
        {
            await WaitForAsync(async () =>
            {
                await using var scope = provider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();
                return await db.Jobs.AsNoTracking()
                    .AnyAsync(job => job.Id == dueJob.Id && job.NextAttemptAt == null);
            });

            await using var verifyScope = provider.CreateAsyncScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();
            var resetJob = await verifyDb.Jobs.AsNoTracking().SingleAsync(job => job.Id == dueJob.Id);
            var resetOutbox = await verifyDb.OutboxMessages.AsNoTracking().SingleAsync(message => message.Id == dueOutbox.Id);
            var untouchedLegacyOutbox = await verifyDb.OutboxMessages.AsNoTracking().SingleAsync(message => message.Id == legacyOutbox.Id);

            Assert.Null(resetJob.NextAttemptAt);
            Assert.Equal(OutboxMessageStatus.Pending, resetOutbox.Status);
            Assert.Null(resetOutbox.PublishedAt);
            Assert.Equal(5, resetOutbox.Version);
            Assert.Equal(OutboxMessageStatus.Published, untouchedLegacyOutbox.Status);
            Assert.Equal(9, untouchedLegacyOutbox.Version);

            await Task.Delay(TimeSpan.FromMilliseconds(100));
            await verifyDb.Entry(resetOutbox).ReloadAsync();
            Assert.Equal(5, resetOutbox.Version);
        }
        finally
        {
            await recovery.StopAsync(CancellationToken.None);
            recovery.Dispose();
        }
    }

    private static JobRecord CreateJob(DateTime now, DateTime? nextAttemptAt)
        => new()
        {
            Id = Guid.NewGuid(),
            Type = "ingest",
            Status = JobStatus.Queued,
            Payload = "{}",
            CorrelationId = Guid.NewGuid().ToString("N"),
            NextAttemptAt = nextAttemptAt,
            AttemptCount = 2,
            CreatedAt = now,
            UpdatedAt = now
        };

    private static OutboxMessageRecord CreateOutbox(JobRecord job, DateTime now, int version)
        => new()
        {
            Id = Guid.NewGuid(),
            Owner = OutboxMessageOwners.Ingest,
            JobId = job.Id,
            MessageType = DocumentIngestMessageNames.MessageType,
            DeduplicationKey = job.Id.ToString("N"),
            RoutingKey = DocumentIngestMessageNames.Queue,
            Payload = job.Payload,
            CorrelationId = job.CorrelationId!,
            Status = OutboxMessageStatus.Published,
            Attempts = 3,
            Version = version,
            CreatedAt = now,
            UpdatedAt = now,
            PublishedAt = now
        };

    private static async Task WaitForAsync(Func<Task<bool>> condition)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < timeoutAt)
        {
            if (await condition())
                return;
            await Task.Delay(10);
        }

        Assert.Fail("The recovery worker did not reach the expected state before timeout.");
    }

    private sealed class TestClock : IClock
    {
        public TestClock(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; }
    }

    private sealed class TestDbContext : LegalAssistantDbContext
    {
        public TestDbContext(DbContextOptions<LegalAssistantDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<DocumentChunk>().Ignore(chunk => chunk.Embedding);
        }
    }
}
