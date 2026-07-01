using BotFramework.Host.Composition.Modules;
using BotFramework.Host.Events.Serialization;
using Games.Poker.Domain.Events;
using Games.Poker.Infrastructure.Modules;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class PokerEventTests
{
    [Fact]
    public void PokerEvents_RoundTripThroughPersistedEventSerializer()
    {
        IDomainEvent[] events =
        [
            new PokerTableCreated("ABCD", 1, 50, 10),
            new PokerPlayerJoined("ABCD", 2, 1, 50, 11),
            new PokerHandStarted("ABCD", 2, 12),
            new PokerHandEnded("ABCD", "showdown", [new PokerPayout(2, 100)], 13),
        ];
        var serializer = Serializer();

        foreach (var original in events)
        {
            var restored = serializer.Deserialize(original.EventType, serializer.Serialize(original));

            Assert.NotNull(restored);
            Assert.Equal(original.GetType(), restored!.GetType());
            Assert.Equal(original.EventType, restored.EventType);
            Assert.Equal(original.OccurredAt, restored.OccurredAt);
        }
    }

    [Fact]
    public void HandEnded_RoundTripPreservesWinnerPayouts()
    {
        var serializer = Serializer();
        var original = new PokerHandEnded("ABCD", "showdown",
            [new PokerPayout(2, 100), new PokerPayout(3, 50)], 13);

        var restored = Assert.IsType<PokerHandEnded>(
            serializer.Deserialize(original.EventType, serializer.Serialize(original)));

        Assert.Equal(original.InviteCode, restored.InviteCode);
        Assert.Equal(original.Reason, restored.Reason);
        Assert.Equal(original.Winners, restored.Winners);
    }

    private static JsonEventSerializer Serializer()
    {
        var loaded = new LoadedModules(
            [new PokerModule()],
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal),
            [],
            []);
        return new JsonEventSerializer(loaded, NullLogger<JsonEventSerializer>.Instance);
    }
}
