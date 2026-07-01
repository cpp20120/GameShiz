using System.Text.Json;
using BotFramework.Sdk.Domain;
using Games.Admin.Domain.Configuration;
using Games.Blackjack.Domain.Events;
using Games.Blackjack.Domain.Models;
using Games.Horse.Domain.Results;
using Games.Meta.Domain.Risk;
using Games.Meta.Domain.Seasons;
using Games.Pick.Domain.Results;
using Games.PixelBattle.Domain.Entities;
using Games.Poker.Domain.Events;
using Games.SecretHitler.Domain.Configuration;
using Games.SecretHitler.Domain.Entities;
using Games.SecretHitler.Domain.Results;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class DomainContractCoverageTests
{
    [Fact]
    public void PixelBattleGrid_RoundTripsTilesAndVersionstamps()
    {
        var grid = RoundTrip(new PixelBattleGrid(["#fff", "#000"], ["v1", "v2"]));

        Assert.Equal(["#fff", "#000"], grid.Tiles);
        Assert.Equal(["v1", "v2"], grid.Versionstamps);
    }

    [Fact]
    public void PickResult_RoundTripsCompleteOutcome()
    {
        var chain = Guid.NewGuid();
        var result = RoundTrip(new PickResult(
            PickError.None, 100, 900, 250, 150, 25, 2, 3, 1, true, 4, chain,
            ["left", "right"], [0, 1]));

        Assert.Equal(100, result.Bet);
        Assert.Equal(900, result.Balance);
        Assert.Equal(250, result.Payout);
        Assert.Equal(150, result.Net);
        Assert.Equal(25, result.StreakBonus);
        Assert.Equal(2, result.StreakBefore);
        Assert.Equal(3, result.StreakAfter);
        Assert.Equal(1, result.PickedIndex);
        Assert.True(result.Won);
        Assert.Equal(4, result.ChainDepth);
        Assert.Equal(chain, result.ChainGuid);
        Assert.Equal(["left", "right"], result.Variants);
        Assert.Equal([0, 1], result.BackedIndices);
    }

    [Fact]
    public void SeasonRewardResult_RoundTripsAuditRows()
    {
        var result = RoundTrip(new SeasonRewardProcessResult(1,
            [new SeasonRewardPaidRow(1, -100, 42, "<winner>", 500)]));

        Assert.Equal(1, result.Paid);
        var row = Assert.Single(result.Rows);
        Assert.Equal(1, row.Place);
        Assert.Equal(-100, row.ChatId);
        Assert.Equal(42, row.UserId);
        Assert.Equal("<winner>", row.DisplayName);
        Assert.Equal(500, row.Amount);
    }

    [Fact]
    public void RiskFlag_RoundTripsResolutionState()
    {
        var now = DateTimeOffset.UtcNow;
        var flag = RoundTrip(new RiskFlag(1, 2, -3, 4, "player", "velocity", "high",
            "resolved", "reason", "{\"count\":3}", now, now.AddMinutes(1), now.AddMinutes(2)));

        Assert.Equal(1, flag.Id);
        Assert.Equal(2, flag.SeasonId);
        Assert.Equal(-3, flag.ChatId);
        Assert.Equal(4, flag.UserId);
        Assert.Equal("player", flag.DisplayName);
        Assert.Equal("velocity", flag.Kind);
        Assert.Equal("high", flag.Severity);
        Assert.Equal("resolved", flag.Status);
        Assert.Equal("reason", flag.Reason);
        Assert.Equal("{\"count\":3}", flag.EvidenceJson);
        Assert.Equal(now, flag.CreatedAt);
        Assert.Equal(now.AddMinutes(1), flag.UpdatedAt);
        Assert.Equal(now.AddMinutes(2), flag.ResolvedAt);
    }

    [Fact]
    public void SecretHitlerSnapshot_RoundTripsGameAndPlayers()
    {
        var snapshot = RoundTrip(new ShGameSnapshot(
            new SecretHitlerGame { InviteCode = "ABCD", HostUserId = 7, BuyIn = 50 },
            [new SecretHitlerPlayer { InviteCode = "ABCD", UserId = 8, DisplayName = "guest" }]));

        Assert.Equal("ABCD", snapshot.Game.InviteCode);
        Assert.Equal(7, snapshot.Game.HostUserId);
        Assert.Equal(50, snapshot.Game.BuyIn);
        Assert.Equal(8, Assert.Single(snapshot.Players).UserId);
    }

    [Fact]
    public void SmallDomainContracts_ExposeConfiguredValues()
    {
        Assert.Equal([1L, 2L], new AdminOptions { Admins = [1, 2] }.Admins);
        Assert.Equal(75, new SecretHitlerOptions { BuyIn = 75 }.BuyIn);
        Assert.Equal(new TodayRaceResult(3, "file"), RoundTrip(new TodayRaceResult(3, "file")));

        var state = RoundTrip(new BlackjackStateMessageSet(1, 2, 3));
        Assert.Equal("blackjack.state_message_set", state.EventType);
        Assert.Equal(1, state.UserId);
        Assert.Equal(2, state.StateMessageId);
        Assert.Equal(3, state.OccurredAt);
    }

    [Fact]
    public void BlackjackEvents_RoundTripAllPersistedFields()
    {
        IDomainEvent[] events =
        [
            new BlackjackHandClosed(1, 2, 3, "done", 4),
            new BlackjackHandCompleted(1, 2, 10, 20, 19, 18, "win", true, 3),
            new BlackjackHandStarted(1, 2, 10, "AS", "KH", "deck", 4, 5, 6),
            new BlackjackHandUpdated(1, 2, 10, "AS,2C", "KH", "deck2", 4, 5, 7),
        ];

        foreach (var domainEvent in events)
        {
            var json = JsonSerializer.Serialize(domainEvent, domainEvent.GetType());
            var restored = Assert.IsAssignableFrom<IDomainEvent>(
                JsonSerializer.Deserialize(json, domainEvent.GetType()));
            Assert.Equal(domainEvent.EventType, restored.EventType);
            Assert.Equal(domainEvent.OccurredAt, restored.OccurredAt);
        }
    }

    [Fact]
    public void ConcurrencyException_ConstructorsPreserveContext()
    {
        Assert.IsType<ConcurrencyException>(new ConcurrencyException());
        Assert.Equal("conflict", new ConcurrencyException("conflict").Message);
        var inner = new InvalidOperationException("inner");
        Assert.Same(inner, new ConcurrencyException("conflict", inner).InnerException);
        Assert.Equal("Stream game-1 expected version 2, was 3",
            new ConcurrencyException("game-1", 2, 3).Message);
    }

    private static T RoundTrip<T>(T value)
    {
        var json = JsonSerializer.Serialize(value);
        return JsonSerializer.Deserialize<T>(json)!;
    }
}
