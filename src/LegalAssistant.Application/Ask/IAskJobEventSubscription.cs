using System.Threading.Channels;
using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Ask;

public interface IAskJobEventSubscription : IAsyncDisposable
{
    ChannelReader<AskJobEventRecord> Reader { get; }
}
