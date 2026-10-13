using LegalAssistant.Application.Ask;
using LegalAssistant.Application.Common;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Ask;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.BackendTests.Ask;

public sealed class EfAskJobRepositoryTests
{
    [Fact]
    public async Task TryClaimNext_ShouldDistributeQueuedJobsAndRejectActiveDuplicateClaims()
    {
        var clock = new TestClock(new DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc));
        await using var db = CreateDbContext();
        db.AskJobs.AddRange(CreateJob(clock.UtcNow), CreateJob(clock.UtcNow.AddSeconds(1)));
        await db.SaveChangesAsync();

        var repository = new EfAskJobRepository(db, clock);
        var first = await repository.TryClaimNextAsync(TimeSpan.FromMinutes(2));
        var second = await repository.TryClaimNextAsync(TimeSpan.FromMinutes(2));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first!.JobId, second!.JobId);
        Assert.Null(await repository.TryClaimNextAsync(TimeSpan.FromMinutes(2)));
        Assert.Null(await repository.TryClaimAsync(first.JobId, TimeSpan.FromMinutes(2)));
    }

    [Fact]
    public async Task RecoverExpired_ShouldRequeueOnlyTheExpiredLeaseAndInvalidateTheOldOwner()
    {
        var clock = new TestClock(new DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc));
        await using var db = CreateDbContext();
        var job = CreateJob(clock.UtcNow);
        db.AskJobs.Add(job);
        await db.SaveChangesAsync();

        var repository = new EfAskJobRepository(db, clock);
        var lease = await repository.TryClaimAsync(job.Id, TimeSpan.FromMinutes(1));
        clock.UtcNow = clock.UtcNow.AddMinutes(2);

        Assert.Equal(1, await repository.RecoverExpiredAsync(25));
        Assert.False(await repository.RenewLeaseAsync(job.Id, lease!.LeaseId, TimeSpan.FromMinutes(1)));
        Assert.Equal(AskJobStatus.Queued, job.Status);
        Assert.Null(job.LeaseId);

        var replacement = await repository.TryClaimAsync(job.Id, TimeSpan.FromMinutes(1));
        Assert.NotNull(replacement);
        Assert.NotEqual(lease.LeaseId, replacement!.LeaseId);
        Assert.Equal(2, job.AttemptCount);
    }

    private static TestDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<LegalAssistantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static AskJobRecord CreateJob(DateTime now)
        => new()
        {
            Id = Guid.NewGuid(),
            ActorScopeKey = "test",
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            Question = "What is the answer?",
            TopK = 5,
            RequestHash = "hash",
            Status = AskJobStatus.Queued,
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
        public TestDbContext(DbContextOptions<LegalAssistantDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Ignore<Document>();
            modelBuilder.Ignore<DocumentChunk>();
            modelBuilder.Ignore<ChunkingRun>();
            modelBuilder.Ignore<JobRecord>();
            modelBuilder.Ignore<AskJobEventRecord>();
            modelBuilder.Ignore<OutboxMessageRecord>();
            modelBuilder.Ignore<RagPromptTemplate>();
            modelBuilder.Ignore<User>();
            modelBuilder.Ignore<Role>();
            modelBuilder.Ignore<UserRole>();
            modelBuilder.Ignore<RefreshToken>();
            modelBuilder.Ignore<AuthSessionRecord>();
            modelBuilder.Ignore<DataProtectionKeyRecord>();

            modelBuilder.Entity<AskJobRecord>(b =>
            {
                b.ToTable("ask_jobs");
                b.HasKey(x => x.Id);
                b.Ignore(x => x.OwnerUser);
                b.Property(x => x.ActorScopeKey).IsRequired();
                b.Property(x => x.IdempotencyKey).IsRequired();
                b.Property(x => x.Question).IsRequired();
                b.Property(x => x.RequestHash).IsRequired();
            });
        }
    }
}
