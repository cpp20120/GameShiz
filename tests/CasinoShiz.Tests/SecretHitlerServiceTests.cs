using BotFramework.Sdk.Execution;
using Games.SecretHitler.Application.Execution;
using Games.SecretHitler.Domain.Entities;
using Games.SecretHitler.Domain.Results;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class SecretHitlerServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateDecision_DebitsBuyInAndIsDeterministic()
    {
        var command = new ShCreateCommand(1, "alice", -100, 1, "create", 50, [new(1, 1)]);
        var input = Input(command, new(null, [], 100, false, false),
            new Dictionary<string, double> { [SecretHitlerExecutionRules.InviteEntropy] = 0.5 });

        var first = new ShCreateAction().Decide(input);
        var second = new ShCreateAction().Decide(input);

        Assert.Equal(first.Result, second.Result);
        Assert.Equal(DecisionStatus.Accepted, first.Status);
        Assert.Equal(50, Assert.IsType<WalletEconomyEffect>(Assert.Single(first.CustomEffects!)).Amount);
        Assert.Single(first.NewState.Players);
        Assert.Single(first.Events);
    }

    [Theory]
    [InlineData(49, false, false, ShError.NotEnoughCoins)]
    [InlineData(100, true, false, ShError.AlreadyInGame)]
    [InlineData(100, false, true, ShError.GameInProgress)]
    public void CreateDecision_RejectsInvalidState(int balance, bool member, bool chatBusy, ShError error)
    {
        var command = new ShCreateCommand(1, "alice", -100, 1, "create", 50, [new(1, 1)]);
        var decision = new ShCreateAction().Decide(Input(command,
            new(null, [], balance, member, chatBusy),
            new Dictionary<string, double> { [SecretHitlerExecutionRules.InviteEntropy] = 0.5 }));
        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(error, decision.Result.Error);
        Assert.Null(decision.CustomEffects);
    }

    [Fact]
    public void JoinDecision_UsesFirstFreePositionAndDebitsAtomically()
    {
        var state = Lobby();
        state.Players.Add(Player(3, 2));
        var command = new ShJoinCommand("ABCDE", 2, "bob", -100, 2, "join", 50, [new(2, 2)]);

        var decision = new ShJoinAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(1, decision.NewState.Players.Single(player => player.UserId == 2).Position);
        Assert.Equal(100, decision.NewState.Game!.Pot);
        Assert.Single(decision.CustomEffects!);
    }

    [Fact]
    public void StartDecision_UsesFrameworkEntropyForRolesAndDeck()
    {
        var state = Lobby();
        for (var user = 2; user <= 5; user++) state.Players.Add(Player(user, (int)user - 1));
        var command = new ShStartCommand("ABCDE", 1, "p1", -100, 1, "start", []);
        var entropy = SecretHitlerExecutionRules.RoleEntropyNames
            .Concat(SecretHitlerExecutionRules.DeckEntropyNames)
            .ToDictionary(name => name, _ => 0.25);

        var first = new ShStartAction().Decide(Input(command, state, entropy));
        var second = new ShStartAction().Decide(Input(command, state, entropy));

        Assert.Equal(DecisionStatus.Accepted, first.Status);
        Assert.Equal(first.NewState.Game!.DeckState, second.NewState.Game!.DeckState);
        Assert.Equal(first.NewState.Players.Select(player => player.Role),
            second.NewState.Players.Select(player => player.Role));
        Assert.Contains(first.NewState.Players, player => player.Role == ShRole.Hitler);
    }

    [Fact]
    public void LeaveDecision_RefundsAndClosesLastPlayerAtomically()
    {
        var state = Lobby();
        var command = new ShLeaveCommand("ABCDE", 1, "p1", -100, 1, "leave", [new(1, 1)]);

        var decision = new ShLeaveAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.True(decision.Result.GameClosed);
        Assert.Equal(ShStatus.Closed, decision.NewState.Game!.Status);
        Assert.Equal(50, Assert.IsType<WalletEconomyEffect>(Assert.Single(decision.CustomEffects!)).Amount);
    }

    [Fact]
    public void EnactDecision_CompletesGameAndSplitsPotAcrossWinningWallets()
    {
        var state = Lobby();
        state.Game!.Status = ShStatus.Active;
        state.Game.Phase = ShPhase.LegislativeChancellor;
        state.Game.FascistPolicies = 5;
        state.Game.NominatedChancellorPosition = 1;
        state.Game.ChancellorReceived = "FL";
        state.Game.Pot = 101;
        state.Players[0].Role = ShRole.Fascist;
        state.Players.Add(Player(2, 1));
        state.Players[1].Role = ShRole.Hitler;
        var command = new ShEnactCommand("ABCDE", 2, "p2", -100, 2, "enact", 0,
            [new(1, 1), new(2, 2)]);

        var decision = new ShEnactAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(ShStatus.Completed, decision.NewState.Game!.Status);
        Assert.Equal(0, decision.NewState.Game.Pot);
        Assert.Equal(2, decision.CustomEffects!.Count);
        Assert.Equal(101, decision.CustomEffects.Cast<WalletEconomyEffect>().Sum(effect => effect.Amount));
        Assert.Single(decision.Events);
    }

    private static SecretHitlerExecutionState Lobby() => new(
        new SecretHitlerGame
        {
            InviteCode = "ABCDE", HostUserId = 1, ChatId = -100,
            Status = ShStatus.Lobby, BuyIn = 50, Pot = 50,
            CreatedAt = Now.ToUnixTimeMilliseconds(), LastActionAt = Now.ToUnixTimeMilliseconds(),
        },
        [Player(1, 0)], 100, false, false);

    private static SecretHitlerPlayer Player(long userId, int position) => new()
    {
        InviteCode = "ABCDE", UserId = userId, Position = position,
        DisplayName = $"p{userId}", ChatId = userId, IsAlive = true,
        JoinedAt = Now.ToUnixTimeMilliseconds(),
    };

    private static GameActionInput<SecretHitlerExecutionState, TCommand> Input<TCommand>(
        TCommand command, SecretHitlerExecutionState state,
        IReadOnlyDictionary<string, double>? entropy = null) =>
        new(command, state, new WalletSnapshot(0), new Dictionary<string, QuotaSnapshot>(),
            new EntropyValue(entropy ?? new Dictionary<string, double>()), Now);
}
