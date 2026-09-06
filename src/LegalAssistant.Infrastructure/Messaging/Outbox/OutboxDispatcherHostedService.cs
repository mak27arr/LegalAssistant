using LegalAssistant.Application.Common;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Infrastructure.Messaging.Outbox;

public sealed class OutboxDispatcherHostedService : BackgroundService
{
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StaleLease = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RetryBaseDelay = TimeSpan.FromSeconds(10);
    private const int BatchSize = 25;
    private const int MaxAttempts = 10;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxDispatcherHostedService> _logger;
    private readonly IClock _clock;

    public OutboxDispatcherHostedService(
        IServiceProvider serviceProvider,
        ILogger<OutboxDispatcherHostedService> logger,
        IClock clock)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();
                var publishers = scope.ServiceProvider
                    .GetServices<IOutboxMessagePublisher>()
                    .ToArray();
                var messageTypes = publishers
                    .SelectMany(x => x.MessageTypes)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                foreach (var maintenance in scope.ServiceProvider.GetServices<IOutboxMaintenance>())
                    await maintenance.ExecuteAsync(db, stoppingToken);

                await DispatchBatchAsync(db, publishers, messageTypes, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox dispatcher cycle failed");
            }

            try
            {
                await Task.Delay(PollDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task DispatchBatchAsync(
        LegalAssistantDbContext db,
        IReadOnlyCollection<IOutboxMessagePublisher> publishers,
        IReadOnlyCollection<string> messageTypes,
        CancellationToken cancellationToken)
    {
        if (messageTypes.Count == 0)
            return;

        var publisherByType = publishers
            .SelectMany(publisher => publisher.MessageTypes.Select(type => (type, publisher)))
            .ToDictionary(x => x.type, x => x.publisher, StringComparer.Ordinal);
        var messages = await ClaimDueBatchAsync(db, messageTypes, _clock.UtcNow, cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                if (!publisherByType.TryGetValue(message.MessageType, out var publisher))
                    throw new InvalidOperationException($"No outbox publisher is registered for '{message.MessageType}'.");

                await publisher.PublishAsync(message, cancellationToken);
                await MarkPublishedAsync(db, message.Id, _clock.UtcNow, cancellationToken);
                _logger.LogInformation(
                    "Published outbox message. messageType={MessageType} messageId={MessageId} outboxId={OutboxId}",
                    message.MessageType,
                    message.DeduplicationKey ?? message.Id.ToString("N"),
                    message.Id);
            }
            catch (Exception ex)
            {
                var attempt = message.Attempts + 1;
                var terminal = attempt >= MaxAttempts;
                await MarkRetryAsync(
                    db,
                    message.Id,
                    ex.Message,
                    _clock.UtcNow.Add(CalculateBackoff(attempt)),
                    terminal,
                    _clock.UtcNow,
                    cancellationToken);
                _logger.LogWarning(
                    ex,
                    "Failed to publish outbox message. messageType={MessageType} outboxId={OutboxId} attempt={Attempt}",
                    message.MessageType,
                    message.Id,
                    attempt);
            }
        }
    }

    private static async Task<IReadOnlyList<OutboxMessageRecord>> ClaimDueBatchAsync(
        LegalAssistantDbContext db,
        IReadOnlyCollection<string> messageTypes,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var staleBefore = utcNow - StaleLease;
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var messages = await db.OutboxMessages
            .Where(x => messageTypes.Contains(x.MessageType))
            .Where(x =>
                (x.Status == OutboxMessageStatus.Pending && (x.NextAttemptAt == null || x.NextAttemptAt <= utcNow)) ||
                (x.Status == OutboxMessageStatus.Processing && x.UpdatedAt <= staleBefore))
            .OrderBy(x => x.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            await tx.CommitAsync(cancellationToken);
            return messages;
        }

        foreach (var message in messages)
        {
            message.Status = OutboxMessageStatus.Processing;
            message.UpdatedAt = utcNow;
            message.Version += 1;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return messages;
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(cancellationToken);
            return [];
        }
    }

    private static async Task MarkPublishedAsync(
        LegalAssistantDbContext db,
        Guid id,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var message = await db.OutboxMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (message is null)
            return;

        message.Status = OutboxMessageStatus.Published;
        message.PublishedAt = utcNow;
        message.UpdatedAt = utcNow;
        message.Version += 1;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task MarkRetryAsync(
        LegalAssistantDbContext db,
        Guid id,
        string error,
        DateTime nextAttemptAt,
        bool terminal,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var message = await db.OutboxMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (message is null)
            return;

        message.Attempts += 1;
        message.LastError = error;
        message.UpdatedAt = utcNow;
        message.NextAttemptAt = nextAttemptAt;
        message.Status = terminal ? OutboxMessageStatus.Failed : OutboxMessageStatus.Pending;
        message.Version += 1;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static TimeSpan CalculateBackoff(int attempt)
    {
        var multiplier = Math.Min(8, Math.Max(0, attempt - 1));
        return TimeSpan.FromSeconds(RetryBaseDelay.TotalSeconds * Math.Pow(2, multiplier));
    }
}
