using System.Threading.Channels;

namespace Games.PixelBattle.Infrastructure.Integrations;

public sealed class PixelBattleBroadcaster
{
    private readonly Lock _gate = new();
    private readonly List<Channel<PixelBattleUpdate>> _subscribers = [];

    public PixelBattleSubscription Subscribe()
    {
        var channel = Channel.CreateUnbounded<PixelBattleUpdate>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        lock (_gate)
        {
            _subscribers.Add(channel);
        }

        return new PixelBattleSubscription(channel.Reader, () => Unsubscribe(channel));
    }

    public void Broadcast(PixelBattleUpdate update)
    {
        Channel<PixelBattleUpdate>[] subscribers;
        lock (_gate)
        {
            subscribers = [.. _subscribers];
        }

        var staleSubscribers = subscribers
            .Where(subscriber => !subscriber.Writer.TryWrite(update))
            .ToArray();
        foreach (var subscriber in staleSubscribers)
            Unsubscribe(subscriber);
    }

    private void Unsubscribe(Channel<PixelBattleUpdate> channel)
    {
        lock (_gate)
        {
            _subscribers.Remove(channel);
        }

        channel.Writer.TryComplete();
    }
}
