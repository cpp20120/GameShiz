using BotFramework.Host.Events.Serialization;
using BotFramework.Host.Composition.Modules;
using Games.SecretHitler.Domain.Events;
using Games.SecretHitler.Domain.Results;
using Games.SecretHitler.Infrastructure.Modules;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class SecretHitlerEventTests
{
    public static TheoryData<IDomainEvent, string> Events => new()
    {
        { new SecretHitlerGameCreated("ABCD", 42, 100, 1), "sh.game_created" },
        { new SecretHitlerPlayerJoined("ABCD", 43, 2, 100, 2), "sh.player_joined" },
        { new SecretHitlerGameStarted("ABCD", 7, 3), "sh.game_started" },
        { new SecretHitlerGameEnded("ABCD", ShWinner.Liberals, ShWinReason.LiberalPolicies, [new(42, 250), new(43, 125)], 4), "sh.game_ended" },
    };

    [Theory]
    [MemberData(nameof(Events))]
    public void EventContract_HasStableTypeAndTimestamp(IDomainEvent ev, string expectedType)
    {
        Assert.Equal(expectedType, ev.EventType);
        Assert.True(ev.OccurredAt > 0);
    }

    [Theory]
    [MemberData(nameof(Events))]
    public void JsonEventSerializer_RoundTripsEverySecretHitlerEvent(IDomainEvent original, string expectedType)
    {
        var serializer = Serializer();

        var payload = serializer.Serialize(original);
        var restored = serializer.Deserialize(expectedType, payload);

        Assert.NotNull(restored);
        Assert.Equal(original.GetType(), restored!.GetType());
        Assert.Equal(expectedType, restored.EventType);
        Assert.Equal(original.OccurredAt, restored.OccurredAt);
    }

    [Fact]
    public void GameEnded_RoundTripPreservesWinnerReasonAndPayouts()
    {
        var serializer = Serializer();
        var original = new SecretHitlerGameEnded(
            "ABCD",
            ShWinner.Fascists,
            ShWinReason.HitlerElected,
            [new(42, 500), new(43, 250)],
            99);

        var restored = Assert.IsType<SecretHitlerGameEnded>(
            serializer.Deserialize(original.EventType, serializer.Serialize(original)));

        Assert.Equal(original.InviteCode, restored.InviteCode);
        Assert.Equal(original.Winner, restored.Winner);
        Assert.Equal(original.Reason, restored.Reason);
        Assert.Equal(original.Payouts, restored.Payouts);
    }

    private static JsonEventSerializer Serializer()
    {
        var loaded = new LoadedModules(
            [new SecretHitlerModule()],
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal),
            [],
            []);
        return new JsonEventSerializer(loaded, NullLogger<JsonEventSerializer>.Instance);
    }
}
