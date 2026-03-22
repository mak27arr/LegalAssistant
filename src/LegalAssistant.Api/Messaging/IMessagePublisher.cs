using System.Threading;
using System.Threading.Tasks;

namespace LegalAssistant.Api.Messaging
{
    public interface IMessagePublisher
    {
        Task PublishAsync(string topic, string key, string payload, CancellationToken cancellationToken = default);
    }
}
