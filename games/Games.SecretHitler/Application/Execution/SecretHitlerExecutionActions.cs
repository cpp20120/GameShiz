using BotFramework.Sdk.Execution;
using static Games.SecretHitler.Domain.Rules.ShResultHelpers;

namespace Games.SecretHitler.Application.Execution;

public static class SecretHitlerExecutionRules
{
    public const string InviteEntropy = "invite-code";
    public static readonly IReadOnlyList<string> RoleEntropyNames =
        Enumerable.Range(0, 9).Select(i => $"role-{i}").ToArray();
    public static readonly IReadOnlyList<string> DeckEntropyNames =
        Enumerable.Range(0, 16).Select(i => $"deck-{i}").ToArray();
    public static readonly IReadOnlyList<string> ReshuffleEntropyNames =
        Enumerable.Range(0, 16).Select(i => $"reshuffle-{i}").ToArray();

    public static SecretHitlerExecutionState Clone(SecretHitlerExecutionState source) =>
        new(source.Game is null ? null : Clone(source.Game), source.Players.Select(Clone).ToList(),
            source.ActorBalance, source.ActorAlreadyInGame, source.ChatAlreadyHasGame);

    public static SecretHitlerGame Clone(SecretHitlerGame game) => new()
    {
        InviteCode = game.InviteCode, HostUserId = game.HostUserId, ChatId = game.ChatId,
        Status = game.Status, Phase = game.Phase, LiberalPolicies = game.LiberalPolicies,
        FascistPolicies = game.FascistPolicies, ElectionTracker = game.ElectionTracker,
        CurrentPresidentPosition = game.CurrentPresidentPosition,
        NominatedChancellorPosition = game.NominatedChancellorPosition,
        LastElectedPresidentPosition = game.LastElectedPresidentPosition,
        LastElectedChancellorPosition = game.LastElectedChancellorPosition,
        DeckState = game.DeckState, DiscardState = game.DiscardState,
        PresidentDraw = game.PresidentDraw, ChancellorReceived = game.ChancellorReceived,
        Winner = game.Winner, WinReason = game.WinReason, BuyIn = game.BuyIn, Pot = game.Pot,
        StateMessageId = game.StateMessageId, CreatedAt = game.CreatedAt, LastActionAt = game.LastActionAt,
    };

    public static SecretHitlerPlayer Clone(SecretHitlerPlayer player) => new()
    {
        InviteCode = player.InviteCode, Position = player.Position, UserId = player.UserId,
        DisplayName = player.DisplayName, ChatId = player.ChatId, Role = player.Role,
        IsAlive = player.IsAlive, LastVote = player.LastVote,
        StateMessageId = player.StateMessageId, JoinedAt = player.JoinedAt,
    };

    public static string InviteCode(double entropy)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var value = (int)(entropy * 33_554_432d);
        Span<char> chars = stackalloc char[5];
        for (var index = chars.Length - 1; index >= 0; index--)
        {
            chars[index] = alphabet[value & 31];
            value >>= 5;
        }
        return new string(chars);
    }

    public static IReadOnlyList<IGameEffect> Settle(
        SecretHitlerExecutionState state, List<SecretHitlerPayout> payouts)
    {
        var game = state.Game!;
        var winners = game.Winner switch
        {
            ShWinner.Liberals => state.Players.Where(p => p.Role == ShRole.Liberal).ToList(),
            ShWinner.Fascists => state.Players.Where(p => p.Role is ShRole.Fascist or ShRole.Hitler).ToList(),
            _ => [],
        };
        if (winners.Count == 0 || game.Pot == 0) return [];
        var share = game.Pot / winners.Count;
        var remainder = game.Pot - share * winners.Count;
        var effects = new List<IGameEffect>(winners.Count);
        foreach (var winner in winners)
        {
            var amount = share + (remainder-- > 0 ? 1 : 0);
            effects.Add(WalletEconomyEffect.Credit(winner.UserId, winner.ChatId, amount, "sh.winnings"));
            payouts.Add(new(winner.UserId, amount));
        }
        game.Pot = 0;
        return effects;
    }

    public static ShGameSnapshot Snapshot(SecretHitlerExecutionState state) => new(state.Game!, state.Players);
}

public sealed class ShCreateAction : IGameAction<ShCreateCommand, SecretHitlerExecutionState, ShCreateResult>
{
    public GameDecision<SecretHitlerExecutionState, ShCreateResult> Decide(
        GameActionInput<SecretHitlerExecutionState, ShCreateCommand> input)
    {
        if (input.State.ActorBalance < input.Command.BuyIn) return Reject(input.State, ShError.NotEnoughCoins);
        if (input.State.ActorAlreadyInGame) return Reject(input.State, ShError.AlreadyInGame);
        if (input.State.ChatAlreadyHasGame) return Reject(input.State, ShError.GameInProgress);
        var now = input.UtcNow.ToUnixTimeMilliseconds();
        var code = SecretHitlerExecutionRules.InviteCode(input.Entropy.GetDouble(SecretHitlerExecutionRules.InviteEntropy));
        var game = new SecretHitlerGame
        {
            InviteCode = code, HostUserId = input.Command.ActorUserId, ChatId = input.Command.PublicChatId,
            Status = ShStatus.Lobby, Phase = ShPhase.None, BuyIn = input.Command.BuyIn,
            Pot = input.Command.BuyIn, CreatedAt = now, LastActionAt = now,
        };
        var player = new SecretHitlerPlayer
        {
            InviteCode = code, Position = 0, UserId = input.Command.ActorUserId,
            DisplayName = input.Command.DisplayName, ChatId = input.Command.ActorChatId,
            IsAlive = true, JoinedAt = now,
        };
        return new(DecisionStatus.Accepted,
            new(game, [player], input.State.ActorBalance, true, true),
            new(ShError.None, code, input.Command.BuyIn), [], [], [],
            [new SecretHitlerGameCreated(code, input.Command.ActorUserId, input.Command.BuyIn, now)], [],
            CustomEffects: [WalletEconomyEffect.Debit(input.Command.ActorUserId,
                input.Command.ActorChatId, input.Command.BuyIn, "sh.create")]);
    }

    private static GameDecision<SecretHitlerExecutionState, ShCreateResult> Reject(
        SecretHitlerExecutionState state, ShError error) =>
        new(DecisionStatus.Rejected, state, CreateFail(error), [], [], [], [], [], error.ToString());
}

public sealed class ShJoinAction : IGameAction<ShJoinCommand, SecretHitlerExecutionState, ShJoinResult>
{
    public GameDecision<SecretHitlerExecutionState, ShJoinResult> Decide(
        GameActionInput<SecretHitlerExecutionState, ShJoinCommand> input)
    {
        if (input.State.ActorBalance < input.Command.BuyIn) return Reject(input.State, ShError.NotEnoughCoins);
        if (input.State.ActorAlreadyInGame) return Reject(input.State, ShError.AlreadyInGame);
        if (input.State.Game is not { } source || source.Status is ShStatus.Closed or ShStatus.Completed)
            return Reject(input.State, ShError.GameNotFound);
        if (source.Status != ShStatus.Lobby) return Reject(input.State, ShError.GameInProgress);
        if (input.State.Players.Count >= ShRoleDealer.MaxPlayers) return Reject(input.State, ShError.GameFull);

        var state = SecretHitlerExecutionRules.Clone(input.State);
        var position = 0;
        var used = state.Players.Select(p => p.Position).ToHashSet();
        while (used.Contains(position)) position++;
        var now = input.UtcNow.ToUnixTimeMilliseconds();
        state.Players.Add(new SecretHitlerPlayer
        {
            InviteCode = source.InviteCode, Position = position, UserId = input.Command.ActorUserId,
            DisplayName = input.Command.DisplayName, ChatId = input.Command.ActorChatId,
            IsAlive = true, JoinedAt = now,
        });
        state.Game!.Pot += input.Command.BuyIn;
        state.Game.LastActionAt = now;
        return new(DecisionStatus.Accepted, state,
            new(ShError.None, SecretHitlerExecutionRules.Snapshot(state), state.Players.Count, ShRoleDealer.MaxPlayers),
            [], [], [], [new SecretHitlerPlayerJoined(source.InviteCode, input.Command.ActorUserId,
                position, input.Command.BuyIn, now)], [],
            CustomEffects: [WalletEconomyEffect.Debit(input.Command.ActorUserId,
                input.Command.ActorChatId, input.Command.BuyIn, "sh.join")]);
    }

    private static GameDecision<SecretHitlerExecutionState, ShJoinResult> Reject(
        SecretHitlerExecutionState state, ShError error) =>
        new(DecisionStatus.Rejected, state, JoinFail(error), [], [], [], [], [], error.ToString());
}

public sealed class ShStartAction : IGameAction<ShStartCommand, SecretHitlerExecutionState, ShStartResult>
{
    public GameDecision<SecretHitlerExecutionState, ShStartResult> Decide(
        GameActionInput<SecretHitlerExecutionState, ShStartCommand> input)
    {
        if (input.State.Game is not { } source || input.State.Players.All(p => p.UserId != input.Command.ActorUserId))
            return Reject(input.State, ShError.NotInGame);
        if (source.HostUserId != input.Command.ActorUserId) return Reject(input.State, ShError.NotHost);
        if (source.Status != ShStatus.Lobby) return Reject(input.State, ShError.GameInProgress);
        if (input.State.Players.Count < ShRoleDealer.MinPlayers) return Reject(input.State, ShError.NotEnoughPlayers);
        var state = SecretHitlerExecutionRules.Clone(input.State);
        ShTransitions.StartGame(state.Game!, state.Players,
            SecretHitlerExecutionRules.RoleEntropyNames.Select(input.Entropy.GetDouble).ToArray(),
            SecretHitlerExecutionRules.DeckEntropyNames.Select(input.Entropy.GetDouble).ToArray());
        state.Game!.LastActionAt = input.UtcNow.ToUnixTimeMilliseconds();
        return new(DecisionStatus.Accepted, state,
            new(ShError.None, SecretHitlerExecutionRules.Snapshot(state)), [], [], [],
            [new SecretHitlerGameStarted(source.InviteCode, state.Players.Count, state.Game.LastActionAt)], []);
    }

    private static GameDecision<SecretHitlerExecutionState, ShStartResult> Reject(
        SecretHitlerExecutionState state, ShError error) =>
        new(DecisionStatus.Rejected, state, StartFail(error), [], [], [], [], [], error.ToString());
}

public sealed class ShNominateAction : IGameAction<ShNominateCommand, SecretHitlerExecutionState, ShNominateResult>
{
    public GameDecision<SecretHitlerExecutionState, ShNominateResult> Decide(
        GameActionInput<SecretHitlerExecutionState, ShNominateCommand> input)
    {
        if (input.State.Game is null) return Reject(input.State, ShError.NotInGame);
        var state = SecretHitlerExecutionRules.Clone(input.State);
        var actor = state.Players.FirstOrDefault(p => p.UserId == input.Command.ActorUserId);
        if (actor is null) return Reject(input.State, ShError.NotInGame);
        var validation = ShTransitions.ValidateNomination(state.Game!, actor,
            input.Command.ChancellorPosition, state.Players);
        if (validation != ShValidation.Ok) return Reject(input.State, MapValidation(validation));
        ShTransitions.ApplyNomination(state.Game!, input.Command.ChancellorPosition, state.Players);
        state.Game!.LastActionAt = input.UtcNow.ToUnixTimeMilliseconds();
        return new(DecisionStatus.Accepted, state,
            new(ShError.None, SecretHitlerExecutionRules.Snapshot(state)), [], [], [], [], []);
    }

    private static GameDecision<SecretHitlerExecutionState, ShNominateResult> Reject(
        SecretHitlerExecutionState state, ShError error) =>
        new(DecisionStatus.Rejected, state, NominateFail(error), [], [], [], [], [], error.ToString());
}

public sealed class ShVoteAction : IGameAction<ShVoteCommand, SecretHitlerExecutionState, ShVoteResult>
{
    public GameDecision<SecretHitlerExecutionState, ShVoteResult> Decide(
        GameActionInput<SecretHitlerExecutionState, ShVoteCommand> input)
    {
        if (input.State.Game is null) return Reject(input.State, ShError.NotInGame);
        var state = SecretHitlerExecutionRules.Clone(input.State);
        var actor = state.Players.FirstOrDefault(p => p.UserId == input.Command.ActorUserId);
        if (actor is null) return Reject(input.State, ShError.NotInGame);
        var validation = ShTransitions.ValidateVote(state.Game!, actor);
        if (validation != ShValidation.Ok) return Reject(input.State, MapValidation(validation));
        var after = ShTransitions.ApplyVote(state.Game!, actor, input.Command.Vote, state.Players,
            SecretHitlerExecutionRules.ReshuffleEntropyNames.Select(input.Entropy.GetDouble).ToArray());
        state.Game!.LastActionAt = input.UtcNow.ToUnixTimeMilliseconds();
        var payouts = new List<SecretHitlerPayout>();
        var effects = state.Game.Status == ShStatus.Completed
            ? SecretHitlerExecutionRules.Settle(state, payouts) : [];
        IDomainEvent[] events = state.Game.Status == ShStatus.Completed
            ? [new SecretHitlerGameEnded(state.Game.InviteCode, state.Game.Winner,
                state.Game.WinReason, payouts, state.Game.LastActionAt)] : [];
        return new(DecisionStatus.Accepted, state,
            new(ShError.None, SecretHitlerExecutionRules.Snapshot(state), after),
            [], [], [], events, [], CustomEffects: effects);
    }

    private static GameDecision<SecretHitlerExecutionState, ShVoteResult> Reject(
        SecretHitlerExecutionState state, ShError error) =>
        new(DecisionStatus.Rejected, state, VoteFail(error), [], [], [], [], [], error.ToString());
}

public sealed class ShDiscardAction : IGameAction<ShDiscardCommand, SecretHitlerExecutionState, ShDiscardResult>
{
    public GameDecision<SecretHitlerExecutionState, ShDiscardResult> Decide(
        GameActionInput<SecretHitlerExecutionState, ShDiscardCommand> input)
    {
        if (input.State.Game is null) return Reject(input.State, ShError.NotInGame);
        var state = SecretHitlerExecutionRules.Clone(input.State);
        var actor = state.Players.FirstOrDefault(p => p.UserId == input.Command.ActorUserId);
        if (actor is null) return Reject(input.State, ShError.NotInGame);
        var validation = ShTransitions.ValidatePresidentDiscard(state.Game!, actor, input.Command.DiscardIndex);
        if (validation != ShValidation.Ok) return Reject(input.State, MapValidation(validation));
        ShTransitions.ApplyPresidentDiscard(state.Game!, input.Command.DiscardIndex);
        state.Game!.LastActionAt = input.UtcNow.ToUnixTimeMilliseconds();
        return new(DecisionStatus.Accepted, state,
            new(ShError.None, SecretHitlerExecutionRules.Snapshot(state)), [], [], [], [], []);
    }

    private static GameDecision<SecretHitlerExecutionState, ShDiscardResult> Reject(
        SecretHitlerExecutionState state, ShError error) =>
        new(DecisionStatus.Rejected, state, DiscardFail(error), [], [], [], [], [], error.ToString());
}

public sealed class ShEnactAction : IGameAction<ShEnactCommand, SecretHitlerExecutionState, ShEnactResult>
{
    public GameDecision<SecretHitlerExecutionState, ShEnactResult> Decide(
        GameActionInput<SecretHitlerExecutionState, ShEnactCommand> input)
    {
        if (input.State.Game is null) return Reject(input.State, ShError.NotInGame);
        var state = SecretHitlerExecutionRules.Clone(input.State);
        var actor = state.Players.FirstOrDefault(p => p.UserId == input.Command.ActorUserId);
        if (actor is null) return Reject(input.State, ShError.NotInGame);
        var validation = ShTransitions.ValidateChancellorEnact(state.Game!, actor, input.Command.EnactIndex);
        if (validation != ShValidation.Ok) return Reject(input.State, MapValidation(validation));
        var after = ShTransitions.ApplyChancellorEnact(state.Game!, input.Command.EnactIndex, state.Players);
        state.Game!.LastActionAt = input.UtcNow.ToUnixTimeMilliseconds();
        var payouts = new List<SecretHitlerPayout>();
        var effects = state.Game.Status == ShStatus.Completed
            ? SecretHitlerExecutionRules.Settle(state, payouts) : [];
        IDomainEvent[] events = state.Game.Status == ShStatus.Completed
            ? [new SecretHitlerGameEnded(state.Game.InviteCode, state.Game.Winner,
                state.Game.WinReason, payouts, state.Game.LastActionAt)] : [];
        return new(DecisionStatus.Accepted, state,
            new(ShError.None, SecretHitlerExecutionRules.Snapshot(state), after),
            [], [], [], events, [], CustomEffects: effects);
    }

    private static GameDecision<SecretHitlerExecutionState, ShEnactResult> Reject(
        SecretHitlerExecutionState state, ShError error) =>
        new(DecisionStatus.Rejected, state, EnactFail(error), [], [], [], [], [], error.ToString());
}

public sealed class ShLeaveAction : IGameAction<ShLeaveCommand, SecretHitlerExecutionState, ShLeaveResult>
{
    public GameDecision<SecretHitlerExecutionState, ShLeaveResult> Decide(
        GameActionInput<SecretHitlerExecutionState, ShLeaveCommand> input)
    {
        if (input.State.Game is null) return Reject(input.State, ShError.NotInGame);
        var state = SecretHitlerExecutionRules.Clone(input.State);
        var actor = state.Players.FirstOrDefault(p => p.UserId == input.Command.ActorUserId);
        if (actor is null) return Reject(input.State, ShError.NotInGame);
        if (state.Game!.Status == ShStatus.Active) return Reject(input.State, ShError.GameInProgress);
        state.Game.Pot = Math.Max(0, state.Game.Pot - state.Game.BuyIn);
        state.Game.LastActionAt = input.UtcNow.ToUnixTimeMilliseconds();
        state.Players.Remove(actor);
        var closed = state.Players.Count == 0;
        if (closed) state.Game.Status = ShStatus.Closed;
        return new(DecisionStatus.Accepted, state,
            new(ShError.None, closed ? null : SecretHitlerExecutionRules.Snapshot(state), closed),
            [], [], [], [], [], CustomEffects:
            [WalletEconomyEffect.Credit(actor.UserId, actor.ChatId, state.Game.BuyIn, "sh.leave")]);
    }

    private static GameDecision<SecretHitlerExecutionState, ShLeaveResult> Reject(
        SecretHitlerExecutionState state, ShError error) =>
        new(DecisionStatus.Rejected, state, LeaveFail(error), [], [], [], [], [], error.ToString());
}

public sealed class ShPlayerMessageAction
    : IGameAction<ShPlayerMessageCommand, SecretHitlerExecutionState, bool>
{
    public GameDecision<SecretHitlerExecutionState, bool> Decide(
        GameActionInput<SecretHitlerExecutionState, ShPlayerMessageCommand> input)
    {
        var state = SecretHitlerExecutionRules.Clone(input.State);
        var actor = state.Players.FirstOrDefault(p => p.UserId == input.Command.ActorUserId);
        if (actor is null) return new(DecisionStatus.Rejected, input.State, false, [], [], [], [], [], "not_in_game");
        actor.StateMessageId = input.Command.MessageId;
        return new(DecisionStatus.Accepted, state, true, [], [], [], [], []);
    }
}

public sealed class ShPublicMessageAction
    : IGameAction<ShPublicMessageCommand, SecretHitlerExecutionState, bool>
{
    public GameDecision<SecretHitlerExecutionState, bool> Decide(
        GameActionInput<SecretHitlerExecutionState, ShPublicMessageCommand> input)
    {
        if (input.State.Game is null)
            return new(DecisionStatus.Rejected, input.State, false, [], [], [], [], [], "game_not_found");
        var state = SecretHitlerExecutionRules.Clone(input.State);
        state.Game!.StateMessageId = input.Command.MessageId;
        return new(DecisionStatus.Accepted, state, true, [], [], [], [], []);
    }
}
