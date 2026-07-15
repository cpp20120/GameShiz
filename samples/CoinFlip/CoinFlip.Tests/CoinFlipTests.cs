using BotFramework.Contracts.Tenancy;
using CoinFlip.Application;
using CoinFlip.Contracts;
using Xunit;

namespace CoinFlip.Tests;

public sealed class CoinFlipTests
{
    [Fact]
    public async Task Same_player_and_scope_are_isolated_by_tenant()
    {
        var store = new InMemoryCoinFlipStateStore();
        var service = new CoinFlipService(store);
        var scope = ScopeId.Create("main");
        var player = PlayerId.Create("player-1");

        await service.ExecuteAsync(new(TenantId.Create("tenant-a"), scope, player, "a-1"), 0, default);
        var other = await service.ExecuteAsync(new(TenantId.Create("tenant-b"), scope, player, "b-1"), 1, default);

        Assert.Equal(1, other.Flips);
        Assert.Equal(0, other.Heads);
        Assert.Equal(1, other.Tails);
    }
}
