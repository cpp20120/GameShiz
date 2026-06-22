using System.Threading.Channels;

namespace Games.PixelBattle.Infrastructure.Integrations;

public sealed class PixelBattleSubscription(
    ChannelReader<PixelBattleUpdate> reader,
    Action unsubscribe) : IDisposable
{
    public ChannelReader<PixelBattleUpdate> Reader { get; } = reader;

    public void Dispose() => unsubscribe();
}
