using LegalAssistant.Infrastructure.Db;

namespace LegalAssistant.Infrastructure.Messaging.Outbox;

public interface IOutboxMaintenance
{
    Task ExecuteAsync(
        LegalAssistantDbContext db,
        CancellationToken cancellationToken = default);
}
