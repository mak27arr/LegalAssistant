using LegalAssistant.Application.Common;
using LegalAssistant.Application.Jobs;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using LegalAssistant.Infrastructure.Db.Configurations;
using LegalAssistant.Infrastructure.Jobs;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.BackendTests.Jobs;

public sealed class EfJobRepositoryTests
{
    [Fact]
    public async Task TryMarkInProgress_ShouldRejectActiveLeaseAndReclaimExpiredLease()
    {
        var clock = new TestClock(new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc));
        await using var db = CreateDbContext();
        var job = CreateJob(clock.UtcNow);
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var repository = new EfJobRepository(db, clock);
        var firstLease = await repository.TryMarkInProgressAsync(job.Id, TimeSpan.FromMinutes(2));
        var duplicateLease = await repository.TryMarkInProgressAsync(job.Id, TimeSpan.FromMinutes(2));

        Assert.NotNull(firstLease);
        Assert.Null(duplicateLease);
        Assert.Equal(1, job.AttemptCount);

        clock.UtcNow = clock.UtcNow.AddMinutes(3);
        var reclaimedLease = await repository.TryMarkInProgressAsync(job.Id, TimeSpan.FromMinutes(2));

        Assert.NotNull(reclaimedLease);
        Assert.NotEqual(firstLease!.LeaseId, reclaimedLease!.LeaseId);
        Assert.Equal(2, job.AttemptCount);
    }

    [Fact]
    public async Task RecordFailure_ShouldQueueTransientFailuresAndFailPermanentFailures()
    {
        var clock = new TestClock(new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc));
        await using var db = CreateDbContext();
        var job = CreateJob(clock.UtcNow);
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var repository = new EfJobRepository(db, clock);
        var lease = await repository.TryMarkInProgressAsync(job.Id, TimeSpan.FromMinutes(2));
        var retryResult = await repository.RecordFailureAsync(
            job.Id,
            lease!.LeaseId,
            "HttpRequestException: upstream timed out",
            permanent: false,
            maxAttempts: 3,
            retryDelay: TimeSpan.FromSeconds(5));

        Assert.Equal(JobFailureResult.Retrying, retryResult);
        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Equal("HttpRequestException: upstream timed out", job.LastError);
        Assert.Equal(clock.UtcNow.AddSeconds(5), job.NextAttemptAt);

        clock.UtcNow = job.NextAttemptAt!.Value;
        var nextLease = await repository.TryMarkInProgressAsync(job.Id, TimeSpan.FromMinutes(2));
        var failedResult = await repository.RecordFailureAsync(
            job.Id,
            nextLease!.LeaseId,
            "IngestJobPermanentException: document not found",
            permanent: true,
            maxAttempts: 3,
            retryDelay: TimeSpan.Zero);

        Assert.Equal(JobFailureResult.Failed, failedResult);
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal("IngestJobPermanentException: document not found", job.Result);
        Assert.Null(job.LeaseExpiresAt);
    }

    [Fact]
    public async Task RecordFailure_ShouldFailTransientFailureAtAttemptLimit()
    {
        var clock = new TestClock(new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc));
        await using var db = CreateDbContext();
        var job = CreateJob(clock.UtcNow);
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var repository = new EfJobRepository(db, clock);
        var lease = await repository.TryMarkInProgressAsync(job.Id, TimeSpan.FromMinutes(2));
        var result = await repository.RecordFailureAsync(
            job.Id,
            lease!.LeaseId,
            "HttpRequestException: upstream unavailable",
            permanent: false,
            maxAttempts: 1,
            retryDelay: TimeSpan.FromSeconds(5));

        Assert.Equal(JobFailureResult.Failed, result);
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal("HttpRequestException: upstream unavailable", job.LastError);
        Assert.Null(job.NextAttemptAt);
    }

    private static TestDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<LegalAssistantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new TestDbContext(options);
    }

    private static JobRecord CreateJob(DateTime now)
        => new()
        {
            Id = Guid.NewGuid(),
            Type = "ingest",
            Status = JobStatus.Queued,
            Payload = "{}",
            CorrelationId = "test-correlation",
            CreatedAt = now,
            UpdatedAt = now
        };

    private sealed class TestClock : IClock
    {
        public TestClock(DateTime utcNow) => UtcNow = utcNow;

        public DateTime UtcNow { get; set; }
    }

    private sealed class TestDbContext : LegalAssistantDbContext
    {
        public TestDbContext(DbContextOptions<LegalAssistantDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Ignore<Document>();
            modelBuilder.Ignore<DocumentChunk>();
            modelBuilder.Ignore<ChunkingRun>();
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
            modelBuilder.ApplyConfiguration(new JobRecordConfiguration());
        }
    }
}
