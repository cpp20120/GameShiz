using BotFramework.Sdk.Execution;
using Games.SecretHitler.Application.Execution;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class SecretHitlerExecutionActionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DiscardDecision_ValidatesAndMovesPresidentToChancellorPhase()
    {
        var game = ActiveGame(ShPhase.LegislativePresident);
        game.CurrentPresidentPosition = 0;
        game.PresidentDraw = "LFF";
        var state = State(game, Players());
        var command = new ShDiscardCommand("ABCDE", 100, "p0", 10, 10, "discard", 1, []);

        var decision = new ShDiscardAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(ShError.None, decision.Result.Error);
        Assert.Equal(ShPhase.LegislativeChancellor, decision.NewState.Game!.Phase);
        Assert.Equal("LF", decision.NewState.Game.ChancellorReceived);
        Assert.Equal("F", decision.NewState.Game.DiscardState);
        Assert.Equal(ShPhase.LegislativePresident, state.Game!.Phase);
    }

    [Fact]
    public void DiscardDecision_RejectsInvalidPolicyIndex()
    {
        var game = ActiveGame(ShPhase.LegislativePresident);
        game.CurrentPresidentPosition = 0;
        game.PresidentDraw = "LFF";
        var command = new ShDiscardCommand("ABCDE", 100, "p0", 10, 10, "discard", 3, []);

        var decision = new ShDiscardAction().Decide(Input(command, State(game, Players())));

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(ShError.InvalidPolicy, decision.Result.Error);
    }

    [Fact]
    public void NominateDecision_ValidTargetStartsElection()
    {
        var game = ActiveGame(ShPhase.Nomination);
        game.CurrentPresidentPosition = 0;
        var state = State(game, Players());
        var command = new ShNominateCommand("ABCDE", 100, "p0", 10, 10, "nominate", 1, []);

        var decision = new ShNominateAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(ShPhase.Election, decision.NewState.Game!.Phase);
        Assert.Equal(1, decision.NewState.Game.NominatedChancellorPosition);
    }

    [Fact]
    public void NominateDecision_RejectsActorWhoIsNotPresident()
    {
        var game = ActiveGame(ShPhase.Nomination);
        game.CurrentPresidentPosition = 0;
        var command = new ShNominateCommand("ABCDE", 101, "p1", 10, 10, "nominate", 2, []);

        var decision = new ShNominateAction().Decide(Input(command, State(game, Players())));

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(ShError.NotPresident, decision.Result.Error);
    }

    [Fact]
    public void VoteDecision_WhenHitlerIsElectedSettlesPotAndEmitsEndEvent()
    {
        var game = ActiveGame(ShPhase.Election);
        game.NominatedChancellorPosition = 1;
        game.CurrentPresidentPosition = 0;
        game.FascistPolicies = ShTransitions.HitlerChancellorThreshold;
        game.DeckState = "LLLLLLFFFFFFFFFFF";
        game.Pot = 30;
        var players = Players();
        players[1].Role = ShRole.Hitler;
        players[0].LastVote = ShVote.Ja;
        players[1].LastVote = ShVote.Ja;
        var command = new ShVoteCommand("ABCDE", 102, "p2", 10, 10, "vote", ShVote.Ja, []);
        var entropy = SecretHitlerExecutionRules.ReshuffleEntropyNames
            .ToDictionary(name => name, _ => 0.25);

        var decision = new ShVoteAction().Decide(Input(command, State(game, players), entropy));

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(ShStatus.Completed, decision.NewState.Game!.Status);
        Assert.Equal(ShWinner.Fascists, decision.NewState.Game.Winner);
        var payout = Assert.IsType<WalletEconomyEffect>(Assert.Single(decision.CustomEffects!));
        Assert.Equal(30, payout.Amount);
        var ended = Assert.IsType<SecretHitlerGameEnded>(Assert.Single(decision.Events));
        Assert.Equal(ShWinReason.HitlerElected, ended.Reason);
        Assert.Equal(30, ended.Payouts.Single().Amount);
    }

    [Fact]
    public void VoteDecision_RejectsVoteOutsideElectionPhase()
    {
        var command = new ShVoteCommand("ABCDE", 100, "p0", 10, 10, "vote", ShVote.Ja, []);

        var decision = new ShVoteAction().Decide(Input(
            command, State(ActiveGame(ShPhase.Nomination), Players())));

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(ShError.WrongPhase, decision.Result.Error);
    }

    [Fact]
    public void PlayerMessageDecision_UpdatesOnlyTheActorOnClonedState()
    {
        var state = State(ActiveGame(ShPhase.Nomination), Players());
        var command = new ShPlayerMessageCommand("ABCDE", 100, "p0", 10, 10, "message", 42, []);

        var decision = new ShPlayerMessageAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.True(decision.Result);
        Assert.Equal(42, decision.NewState.Players[0].StateMessageId);
        Assert.Null(state.Players[0].StateMessageId);
    }

    [Fact]
    public void PlayerMessageDecision_RejectsUnknownActor()
    {
        var state = State(ActiveGame(ShPhase.Nomination), Players());
        var command = new ShPlayerMessageCommand("ABCDE", 999, "unknown", 10, 10, "message", 42, []);

        var decision = new ShPlayerMessageAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.False(decision.Result);
        Assert.Equal("not_in_game", decision.RejectionReason);
    }

    [Fact]
    public void PublicMessageDecision_UpdatesGameMessageOnClonedState()
    {
        var state = State(ActiveGame(ShPhase.Nomination), Players());
        var command = new ShPublicMessageCommand("ABCDE", 100, "p0", 10, 10, "message", 77, []);

        var decision = new ShPublicMessageAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.True(decision.Result);
        Assert.Equal(77, decision.NewState.Game!.StateMessageId);
        Assert.Null(state.Game!.StateMessageId);
    }

    [Fact]
    public void PublicMessageDecision_RejectsMissingGame()
    {
        var command = new ShPublicMessageCommand("ABCDE", 100, "p0", 10, 10, "message", 77, []);

        var decision = new ShPublicMessageAction().Decide(Input(command, new(null, [], null, false, false)));

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.False(decision.Result);
        Assert.Equal("game_not_found", decision.RejectionReason);
    }

    private static SecretHitlerExecutionState State(
        SecretHitlerGame game, List<SecretHitlerPlayer> players) =>
        new(game, players, null, false, false);

    private static SecretHitlerGame ActiveGame(ShPhase phase) => new()
    {
        InviteCode = "ABCDE", ChatId = 10, Status = ShStatus.Active,
        Phase = phase, DeckState = "LLLLLLFFFFFFFFFFF", DiscardState = "",
        LastActionAt = Now.ToUnixTimeMilliseconds(), CreatedAt = Now.ToUnixTimeMilliseconds(),
    };

    private static List<SecretHitlerPlayer> Players() =>
        Enumerable.Range(0, 3).Select(position => new SecretHitlerPlayer
        {
            InviteCode = "ABCDE", Position = position, UserId = 100 + position,
            DisplayName = $"p{position}", ChatId = 10, IsAlive = true,
            Role = ShRole.Liberal,
        }).ToList();

    private static GameActionInput<SecretHitlerExecutionState, TCommand> Input<TCommand>(
        TCommand command,
        SecretHitlerExecutionState state,
        IReadOnlyDictionary<string, double>? entropy = null) =>
        new(command, state, new WalletSnapshot(0), new Dictionary<string, QuotaSnapshot>(),
            new EntropyValue(entropy ?? new Dictionary<string, double>()), Now);
}
