namespace LegalAssistant.Infrastructure.Messaging.Outbox;

public interface IOutboxNotificationListener
{
    void Start(CancellationToken cancellationToken);

    Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
