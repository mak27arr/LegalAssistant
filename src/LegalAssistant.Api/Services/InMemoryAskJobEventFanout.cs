using System.Collections.Concurrent;
using System.Threading.Channels;
using LegalAssistant.Application.Ask;
using LegalAssistant.Domain.Models;

namespace LegalAssistant.Api.Services;

public sealed class InMemoryAskJobEventFanout : IAskJobEventFanout
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<AskJobEventRecord>>> _subscribers = new();

    public IAskJobEventSubscription Subscribe(Guid jobId)
    {
        var channel = Channel.CreateBounded<AskJobEventRecord>(new BoundedChannelOptions(128)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest,
            AllowSynchronousContinuations = false
        });

        var subscriptionId = Guid.NewGuid();
        var subscribers = _subscribers.GetOrAdd(jobId, _ => new ConcurrentDictionary<Guid, Channel<AskJobEventRecord>>());
        subscribers[subscriptionId] = channel;

        return new Subscription(jobId, subscriptionId, channel, _subscribers);
    }

    public Task PublishAsync(AskJobEventRecord eventRecord, CancellationToken cancellationToken = default)
    {
        if (_subscribers.TryGetValue(eventRecord.JobId, out var subscribers))
        {
            foreach (var subscriber in subscribers.Values)
            {
                subscriber.Writer.TryWrite(eventRecord);
            }
        }

        return Task.CompletedTask;
    }

    private sealed class Subscription : IAskJobEventSubscription
    {
        private readonly Guid _jobId;
        private readonly Guid _subscriptionId;
        private readonly Channel<AskJobEventRecord> _channel;
        private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<AskJobEventRecord>>> _subscribers;

        public Subscription(
            Guid jobId,
            Guid subscriptionId,
            Channel<AskJobEventRecord> channel,
            ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<AskJobEventRecord>>> subscribers)
        {
            _jobId = jobId;
            _subscriptionId = subscriptionId;
            _channel = channel;
            _subscribers = subscribers;
            Reader = channel.Reader;
        }

        public ChannelReader<AskJobEventRecord> Reader { get; }

        public ValueTask DisposeAsync()
        {
            if (_subscribers.TryGetValue(_jobId, out var subscribers) && subscribers.TryRemove(_subscriptionId, out var channel))
            {
                channel.Writer.TryComplete();
            }

            if (_subscribers.TryGetValue(_jobId, out var remaining) && remaining.IsEmpty)
            {
                _subscribers.TryRemove(_jobId, out _);
            }

            return ValueTask.CompletedTask;
        }
    }
}
