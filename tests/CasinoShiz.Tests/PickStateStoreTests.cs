using Games.Pick.Infrastructure.Persistence;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class PickStateStoreTests
{
    [Fact]
    public void StreakStore_TracksUsersAndChatsIndependently()
    {
        var store = new PickStreakStore();

        Assert.Equal(0, store.Get(1, 10));
        Assert.Equal(1, store.Increment(1, 10));
        Assert.Equal(2, store.Increment(1, 10));
        Assert.Equal(1, store.Increment(1, 20));
        Assert.Equal(1, store.Increment(2, 10));
        Assert.Equal(2, store.Get(1, 10));
    }

    [Fact]
    public void StreakStore_Reset_RemovesOnlyRequestedStreak()
    {
        var store = new PickStreakStore();
        store.Increment(1, 10);
        store.Increment(2, 10);

        store.Reset(1, 10);

        Assert.Equal(0, store.Get(1, 10));
        Assert.Equal(1, store.Get(2, 10));
    }

    [Fact]
    public void ChainStore_TryClaim_IsSingleUse()
    {
        var store = new PickChainStore();
        var state = State(DateTimeOffset.UtcNow.AddMinutes(1));
        store.Add(state);

        Assert.Equal(state, store.TryClaim(state.Id));
        Assert.Null(store.TryClaim(state.Id));
    }

    [Fact]
    public void ChainStore_ExpiredClaim_IsRemovedAndRejected()
    {
        var store = new PickChainStore();
        var state = State(DateTimeOffset.UtcNow.AddMinutes(-1));
        store.Add(state);

        Assert.Null(store.TryClaim(state.Id));
        Assert.Null(store.TryClaim(state.Id));
    }

    [Fact]
    public void ChainStore_AddSameId_ReplacesAndForgetRemoves()
    {
        var store = new PickChainStore();
        var original = State(DateTimeOffset.UtcNow.AddMinutes(1));
        var replacement = original with { StakeForNext = 99 };
        store.Add(original);
        store.Add(replacement);

        Assert.Equal(99, store.TryClaim(original.Id)?.StakeForNext);
        store.Add(original);
        store.Forget(original.Id);
        Assert.Null(store.TryClaim(original.Id));
    }

    private static PickChainState State(DateTimeOffset expiresAt) => new(
        Guid.NewGuid(), 1, 10, "player", 20, 1, ["a", "b"], [0], expiresAt);
}
