using System.Text.Json;
using LegalAssistant.Application.Ask;
using LegalAssistant.Application.Common;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Infrastructure.Ask;

public sealed class AskJobOutboxDispatcherHostedService : BackgroundService
{
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StaleLease = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RetryBaseDelay = TimeSpan.FromSeconds(10);
    private const int BatchSize = 25;
    private const int MaxAttempts = 10;

    private readonly IServiceProvider _sp;
    private readonly ILogger<AskJobOutboxDispatcherHostedService> _logger;
    private readonly IClock _clock;

    public AskJobOutboxDispatcherHostedService(
        IServiceProvider sp,
        ILogger<AskJobOutboxDispatcherHostedService> logger,
        IClock clock)
    {
        _sp = sp;
        _logger = logger;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();
                var publisher = scope.ServiceProvider.GetRequiredService<IAskJobEventPublisher>();

                await DispatchBatchAsync(db, publisher, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ask outbox dispatcher cycle failed");
            }

            await Task.Delay(PollDelay, stoppingToken);
        }
    }

    private async Task DispatchBatchAsync(LegalAssistantDbContext db, IAskJobEventPublisher publisher, CancellationToken cancellationToken)
    {
        var utcNow = _clock.UtcNow;
        var messages = await ClaimDueBatchAsync(db, BatchSize, StaleLease, utcNow, cancellationToken);
        if (messages.Count == 0)
            return;

        foreach (var message in messages)
        {
            try
            {
                var eventRecord = JsonSerializer.Deserialize<AskJobEventRecord>(message.Payload);
                if (eventRecord == null)
                    throw new InvalidOperationException($"Ask outbox payload could not be deserialized. outboxId={message.Id}");

                await publisher.PublishAsync(eventRecord, cancellationToken);
                await MarkPublishedAsync(db, message.Id, _clock.UtcNow, cancellationToken);
                _logger.LogInformation("Published ask outbox message. jobId={JobId} outboxId={OutboxId} status={Status}", message.CorrelationId, message.Id, eventRecord.Status);
            }
            catch (Exception ex)
            {
                var nextAttemptAt = _clock.UtcNow.Add(CalculateBackoff(message.Attempts + 1));
                var terminal = message.Attempts + 1 >= MaxAttempts;
                await MarkRetryAsync(db, message.Id, ex.Message, nextAttemptAt, terminal, _clock.UtcNow, cancellationToken);
                _logger.LogWarning(ex, "Failed to publish ask outbox message. jobId={JobId} outboxId={OutboxId} attempt={Attempt}", message.CorrelationId, message.Id, message.Attempts + 1);
            }
        }
    }

    private static async Task<IReadOnlyList<OutboxMessageRecord>> ClaimDueBatchAsync(
        LegalAssistantDbContext db,
        int batchSize,
        TimeSpan staleLease,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var staleBefore = utcNow - staleLease;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var messages = await db.OutboxMessages
            .Where(x => AskJobMessageNames.MessageTypes.Contains(x.MessageType))
            .Where(x =>
                (x.Status == OutboxMessageStatus.Pending && (x.NextAttemptAt == null || x.NextAttemptAt <= utcNow)) ||
                (x.Status == OutboxMessageStatus.Processing && x.UpdatedAt <= staleBefore))
            .OrderBy(x => x.CreatedAt)
            .Take(batchSize)
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

    private static async Task MarkPublishedAsync(LegalAssistantDbContext db, Guid id, DateTime utcNow, CancellationToken cancellationToken)
    {
        var message = await db.OutboxMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (message == null)
            return;

        message.Status = OutboxMessageStatus.Published;
        message.PublishedAt = utcNow;
        message.UpdatedAt = utcNow;
        message.Version += 1;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task MarkRetryAsync(LegalAssistantDbContext db, Guid id, string error, DateTime nextAttemptAt, bool isTerminal, DateTime utcNow, CancellationToken cancellationToken)
    {
        var message = await db.OutboxMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (message == null)
            return;

        message.Attempts += 1;
        message.LastError = error;
        message.UpdatedAt = utcNow;
        message.NextAttemptAt = nextAttemptAt;
        message.Status = isTerminal ? OutboxMessageStatus.Failed : OutboxMessageStatus.Pending;
        message.Version += 1;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static TimeSpan CalculateBackoff(int attempt)
    {
        var multiplier = Math.Min(8, Math.Max(0, attempt - 1));
        return TimeSpan.FromSeconds(RetryBaseDelay.TotalSeconds * Math.Pow(2, multiplier));
    }
}
