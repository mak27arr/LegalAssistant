using LegalAssistant.Application.Ask;
using LegalAssistant.Application.Ask.Models;
using LegalAssistant.Application.Ask.Services;
using LegalAssistant.Application.Auth;
using LegalAssistant.Api.Services;
using LegalAssistant.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LegalAssistant.BackendTests.Ask;

public sealed class AskJobEventStreamUseCaseTests
{
    [Fact]
    public async Task StreamEventsAsync_ShouldYieldJobNotFound_WhenJobDoesNotExist()
    {
        var mockEvents = new Mock<IAskJobEventQueryService>();
        var mockJobs = new Mock<IAskJobRepository>();
        var mockFanout = new Mock<IAskJobEventFanout>();
        var mockSessions = new Mock<IUserSessionManager>();

        var jobId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        mockJobs
            .Setup(x => x.GetByIdAsync(jobId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AskJobRecord?)null);

        var useCase = new AskJobEventStreamUseCase(mockEvents.Object, mockJobs.Object, mockFanout.Object, mockSessions.Object);

        var items = new List<AskJobStreamItem>();
        await foreach (var item in useCase.StreamEventsAsync(jobId, userId, "session-1", 0, CancellationToken.None))
        {
            items.Add(item);
        }

        var single = Assert.Single(items);
        Assert.Equal(AskJobStreamItemKind.JobNotFound, single.Kind);
    }

    [Fact]
    public async Task StreamEventsAsync_ShouldKeepStreaming_WhenMultipleLiveEventsArrive()
    {
        var mockEvents = new Mock<IAskJobEventQueryService>();
        var mockJobs = new Mock<IAskJobRepository>();
        var mockSessions = new Mock<IUserSessionManager>();
        var fanout = new InMemoryAskJobEventFanout(NullLogger<InMemoryAskJobEventFanout>.Instance);
        var jobId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        mockJobs
            .Setup(x => x.GetByIdAsync(jobId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AskJobRecord
            {
                Id = jobId,
                OwnerUserId = userId,
                ActorScopeKey = $"user:{userId:N}",
                IdempotencyKey = "idempotency-key",
                Question = "Question",
                TopK = 5,
                RequestHash = "request-hash",
                Status = AskJobStatus.Queued
            });
        mockEvents
            .Setup(x => x.GetLatestAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AskJobEventRecord?)null);
        mockEvents
            .Setup(x => x.GetSinceAsync(jobId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AskJobEventRecord>());

        var useCase = new AskJobEventStreamUseCase(
            mockEvents.Object,
            mockJobs.Object,
            fanout,
            mockSessions.Object);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var enumerator = useCase
            .StreamEventsAsync(jobId, userId, "session-1", 0, timeout.Token)
            .GetAsyncEnumerator(timeout.Token);

        var firstMove = enumerator.MoveNextAsync().AsTask();
        await Task.Delay(50, timeout.Token);
        await fanout.PublishAsync(CreateEvent(jobId, 1, AskJobStatus.InProgress), timeout.Token);

        Assert.True(await firstMove.WaitAsync(timeout.Token));
        Assert.Equal(AskJobStatus.InProgress, enumerator.Current.EventRecord?.Status);

        var secondMove = enumerator.MoveNextAsync().AsTask();
        await fanout.PublishAsync(CreateEvent(jobId, 2, AskJobStatus.Completed), timeout.Token);

        Assert.True(await secondMove.WaitAsync(timeout.Token));
        Assert.Equal(AskJobStatus.Completed, enumerator.Current.EventRecord?.Status);
    }

    [Fact]
    public async Task StreamEventsAsync_ShouldReconcileEvents_WhenLiveFanoutMissesThem()
    {
        var mockEvents = new Mock<IAskJobEventQueryService>();
        var mockJobs = new Mock<IAskJobRepository>();
        var mockSessions = new Mock<IUserSessionManager>();
        var fanout = new InMemoryAskJobEventFanout(NullLogger<InMemoryAskJobEventFanout>.Instance);
        var jobId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var missedEvent = CreateEvent(jobId, 1, AskJobStatus.Completed);

        mockJobs
            .Setup(x => x.GetByIdAsync(jobId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AskJobRecord
            {
                Id = jobId,
                OwnerUserId = userId,
                ActorScopeKey = $"user:{userId:N}",
                IdempotencyKey = "idempotency-key",
                Question = "Question",
                TopK = 5,
                RequestHash = "request-hash",
                Status = AskJobStatus.Queued
            });
        mockEvents
            .Setup(x => x.GetLatestAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AskJobEventRecord?)null);
        mockEvents
            .SetupSequence(x => x.GetSinceAsync(jobId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AskJobEventRecord>())
            .ReturnsAsync(new[] { missedEvent });

        var useCase = new AskJobEventStreamUseCase(
            mockEvents.Object,
            mockJobs.Object,
            fanout,
            mockSessions.Object,
            reconciliationInterval: TimeSpan.FromMilliseconds(50));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var enumerator = useCase
            .StreamEventsAsync(jobId, userId, "session-1", 0, timeout.Token)
            .GetAsyncEnumerator(timeout.Token);

        Assert.True(await enumerator.MoveNextAsync().AsTask().WaitAsync(timeout.Token));
        Assert.Equal(AskJobStatus.Completed, enumerator.Current.EventRecord?.Status);
        Assert.True(enumerator.Current.IsReplay);
    }

    private static AskJobEventRecord CreateEvent(Guid jobId, long id, AskJobStatus status) => new()
    {
        Id = id,
        JobId = jobId,
        ActorScopeKey = "actor-scope",
        IdempotencyKey = "idempotency-key",
        Question = "Question",
        TopK = 5,
        Status = status,
        OccurredAtUtc = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow
    };
}
