using System.Data;
using LegalAssistant.Application.Embeddings;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace LegalAssistant.Infrastructure.Embeddings;

public sealed class SerializableEmbeddingStatusService : IEmbeddingStatusService
{
    private const int MaxAttempts = 3;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EmbeddingStatusService _currentAttempt;
    private readonly ILogger<SerializableEmbeddingStatusService> _logger;

    public SerializableEmbeddingStatusService(
        IServiceScopeFactory scopeFactory,
        EmbeddingStatusService currentAttempt,
        ILogger<SerializableEmbeddingStatusService> logger)
    {
        _scopeFactory = scopeFactory;
        _currentAttempt = currentAttempt;
        _logger = logger;
    }

    public Task<bool> MarkInProgressAsync(
        Guid chunkId,
        Guid? jobId,
        Guid? chunkingRunId,
        CancellationToken cancellationToken = default)
        => _currentAttempt.MarkInProgressAsync(chunkId, jobId, chunkingRunId, cancellationToken);

    public Task<EmbeddingStatusUpdateResult> MarkCompletedAsync(
        Guid chunkId,
        float[] vector,
        Guid? jobId,
        Guid? chunkingRunId,
        CancellationToken cancellationToken = default)
        => ExecuteSerializableAsync(
            "complete-chunk",
            chunkId,
            jobId,
            chunkingRunId,
            (attempt, token) => attempt.MarkCompletedAsync(chunkId, vector, jobId, chunkingRunId, token),
            cancellationToken);

    public Task<EmbeddingStatusUpdateResult> RecordFailureAsync(
        Guid chunkId,
        string error,
        bool terminal,
        Guid? jobId,
        Guid? chunkingRunId,
        CancellationToken cancellationToken = default)
        => ExecuteSerializableAsync(
            "record-chunk-failure",
            chunkId,
            jobId,
            chunkingRunId,
            (attempt, token) => attempt.RecordFailureAsync(chunkId, error, terminal, jobId, chunkingRunId, token),
            cancellationToken);

    public Task<EmbeddingStatusUpdateResult> FinalizeRunAsync(
        Guid runId,
        Guid jobId,
        CancellationToken cancellationToken = default)
        => ExecuteSerializableAsync(
            "finalize-run",
            Guid.Empty,
            jobId,
            runId,
            (attempt, token) => attempt.FinalizeRunAsync(runId, jobId, token),
            cancellationToken);

    private async Task<T> ExecuteSerializableAsync<T>(
        string operation,
        Guid chunkId,
        Guid? jobId,
        Guid? runId,
        Func<EmbeddingStatusService, CancellationToken, Task<T>> execute,
        CancellationToken cancellationToken)
    {
        for (var attemptNumber = 1; attemptNumber <= MaxAttempts; attemptNumber++)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();
            var attemptService = scope.ServiceProvider.GetRequiredService<EmbeddingStatusService>();
            IDbContextTransaction? transaction = null;

            try
            {
                if (db.Database.IsRelational())
                    transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

                var result = await execute(attemptService, cancellationToken);
                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                await RollbackBestEffortAsync(transaction, operation, chunkId, jobId, runId, attemptNumber, ex);
                _logger.LogDebug(ex,
                    "Embedding persistence transaction was cancelled; caller will requeue or stop. operation={Operation} chunkId={ChunkId} jobId={JobId} runId={RunId} attempt={Attempt}",
                    operation, chunkId, jobId, runId, attemptNumber);
                throw;
            }
            catch (Exception ex) when (IsSerializationConflict(ex))
            {
                await RollbackBestEffortAsync(transaction, operation, chunkId, jobId, runId, attemptNumber, ex);
                if (attemptNumber == MaxAttempts)
                {
                    _logger.LogError(ex,
                        "Embedding aggregation exhausted bounded serialization retries; propagating for message redelivery. operation={Operation} chunkId={ChunkId} jobId={JobId} runId={RunId} attempt={Attempt}",
                        operation, chunkId, jobId, runId, attemptNumber);
                    throw;
                }

                _logger.LogWarning(ex,
                    "Embedding aggregation transaction conflicted; retrying the full operation with a fresh scope. operation={Operation} chunkId={ChunkId} jobId={JobId} runId={RunId} attempt={Attempt} maxAttempts={MaxAttempts}",
                    operation, chunkId, jobId, runId, attemptNumber, MaxAttempts);
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attemptNumber), cancellationToken);
            }
            catch (Exception ex)
            {
                await RollbackBestEffortAsync(transaction, operation, chunkId, jobId, runId, attemptNumber, ex);
                _logger.LogWarning(ex,
                    "Embedding persistence transaction failed; propagating to the transport retry boundary. operation={Operation} chunkId={ChunkId} jobId={JobId} runId={RunId} attempt={Attempt}",
                    operation, chunkId, jobId, runId, attemptNumber);
                throw;
            }
            finally
            {
                if (transaction is not null)
                    await transaction.DisposeAsync();
            }
        }

        throw new InvalidOperationException("Embedding aggregation retry loop ended unexpectedly.");
    }

    private async Task RollbackBestEffortAsync(
        IDbContextTransaction? transaction,
        string operation,
        Guid chunkId,
        Guid? jobId,
        Guid? runId,
        int attempt,
        Exception originalException)
    {
        if (transaction is null)
            return;

        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception rollbackException)
        {
            _logger.LogError(rollbackException,
                "Embedding aggregation rollback also failed; preserving the original exception. operation={Operation} chunkId={ChunkId} jobId={JobId} runId={RunId} attempt={Attempt} originalException={OriginalException}",
                operation, chunkId, jobId, runId, attempt, originalException);
        }
    }

    private static bool IsSerializationConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            if (current is PostgresException
                {
                    SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected
                })
                return true;
        }

        return false;
    }
}
