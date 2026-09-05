using LegalAssistant.Application.Ask;
using LegalAssistant.Application.Ask.Models;
using LegalAssistant.Application.Ask.Services;
using LegalAssistant.Application.Auth;
using LegalAssistant.Domain.Models;
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
}
