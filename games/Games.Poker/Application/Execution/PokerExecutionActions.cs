using BotFramework.Sdk.Execution;

namespace Games.Poker.Application.Execution;

public static class PokerExecutionRules
{
    public static readonly IReadOnlyList<string> ShuffleEntropyNames =
        Enumerable.Range(0, 51).Select(index => $"shuffle-{index}").ToArray();
    public const string InviteEntropy = "invite-code";

    public static PokerExecutionState Clone(PokerExecutionState state) =>
        new(state.Table is null ? null : Clone(state.Table), state.Seats.Select(Clone).ToList(), state.ActorBalance);

    public static PokerTable Clone(PokerTable table) => new()
    {
        InviteCode = table.InviteCode, ChatId = table.ChatId, HostUserId = table.HostUserId,
        Status = table.Status, Phase = table.Phase, SmallBlind = table.SmallBlind,
        BigBlind = table.BigBlind, Pot = table.Pot, CommunityCards = table.CommunityCards,
        DeckState = table.DeckState, ButtonSeat = table.ButtonSeat, CurrentSeat = table.CurrentSeat,
        CurrentBet = table.CurrentBet, MinRaise = table.MinRaise,
        StateMessageId = table.StateMessageId, LastActionAt = table.LastActionAt,
        CreatedAt = table.CreatedAt,
    };

    public static PokerSeat Clone(PokerSeat seat) => new()
    {
        InviteCode = seat.InviteCode, Position = seat.Position, UserId = seat.UserId,
        DisplayName = seat.DisplayName, Stack = seat.Stack, HoleCards = seat.HoleCards,
        Status = seat.Status, CurrentBet = seat.CurrentBet, TotalCommitted = seat.TotalCommitted,
        HasActedThisRound = seat.HasActedThisRound, ChatId = seat.ChatId,
        StateMessageId = seat.StateMessageId, JoinedAt = seat.JoinedAt,
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

    public static PokerError MapValidation(ValidationResult value) => value switch
    {
        ValidationResult.CannotCheck => PokerError.CannotCheck,
        ValidationResult.RaiseTooSmall => PokerError.RaiseTooSmall,
        ValidationResult.RaiseTooLarge => PokerError.RaiseTooLarge,
        _ => PokerError.InvalidAction,
    };

    public static ActionResolution Resolve(PokerExecutionState state, DateTimeOffset now)
    {
        var table = state.Table!;
        var transition = PokerDomain.ResolveAfterAction(table, state.Seats);
        if (transition.Kind is not (TransitionKind.HandEndedLastStanding
            or TransitionKind.HandEndedRunout or TransitionKind.HandEndedShowdown))
        {
            var kind = transition.Kind == TransitionKind.PhaseAdvanced
                ? HandTransition.PhaseAdvanced : HandTransition.TurnAdvanced;
            return new(new ActionResult(PokerError.None, Snapshot(state), kind, null, null, null), [], []);
        }

        var showdown = transition.Showdown!.ToList();
        var reason = transition.Kind switch
        {
            TransitionKind.HandEndedLastStanding => "last_standing",
            TransitionKind.HandEndedRunout => "runout",
            _ => "showdown",
        };
        var payouts = showdown.Where(entry => entry.Won > 0)
            .Select(entry => (IGameEffect)WalletEconomyEffect.Credit(
                entry.Seat.UserId, entry.Seat.ChatId, entry.Won, "poker.win"))
            .ToArray();
        var winners = showdown.Where(entry => entry.Won > 0)
            .Select(entry => new PokerPayout(entry.Seat.UserId, entry.Won)).ToArray();
        IDomainEvent[] events =
        [
            new PokerHandEnded(table.InviteCode, reason, winners, now.ToUnixTimeMilliseconds()),
        ];
        return new(new ActionResult(PokerError.None, Snapshot(state), HandTransition.HandEnded,
            showdown, null, null), payouts, events);
    }

    public static TableSnapshot Snapshot(PokerExecutionState state) =>
        new(state.Table!, state.Seats);
}

public sealed record ActionResolution(
    ActionResult Result,
    IReadOnlyList<IGameEffect> Effects,
    IReadOnlyList<IDomainEvent> Events);

public sealed class PokerCreateAction
    : IGameAction<PokerCreateCommand, PokerExecutionState, CreateResult>
{
    public GameDecision<PokerExecutionState, CreateResult> Decide(
        GameActionInput<PokerExecutionState, PokerCreateCommand> input)
    {
        if (input.State.Table is not null)
            return Reject(input.State, PokerError.TableAlreadyExists, input.Command.BuyIn);
        if (input.State.ActorBalance < input.Command.BuyIn)
            return Reject(input.State, PokerError.NotEnoughCoins, input.Command.BuyIn);

        var command = input.Command;
        var now = input.UtcNow.ToUnixTimeMilliseconds();
        var code = PokerExecutionRules.InviteCode(input.Entropy.GetDouble(PokerExecutionRules.InviteEntropy));
        var table = new PokerTable
        {
            InviteCode = code, ChatId = command.ChatId, HostUserId = command.ActorUserId,
            Status = PokerTableStatus.Seating, Phase = PokerPhase.None,
            SmallBlind = command.SmallBlind, BigBlind = command.BigBlind,
            CreatedAt = now, LastActionAt = now,
        };
        var seat = new PokerSeat
        {
            InviteCode = code, Position = 0, UserId = command.ActorUserId,
            DisplayName = command.DisplayName, Stack = command.BuyIn,
            ChatId = command.ChatId, JoinedAt = now,
        };
        return new(DecisionStatus.Accepted, new(table, [seat], input.State.ActorBalance),
            new(PokerError.None, code, command.BuyIn), [], [], [],
            [new PokerTableCreated(code, command.ActorUserId, command.BuyIn, now)], [],
            CustomEffects:
            [WalletEconomyEffect.Debit(command.ActorUserId, command.ChatId, command.BuyIn, "poker.create")]);
    }

    private static GameDecision<PokerExecutionState, CreateResult> Reject(
        PokerExecutionState state, PokerError error, int buyIn) =>
        new(DecisionStatus.Rejected, state, new(error, "", buyIn), [], [], [], [], [], error.ToString());
}

public sealed class PokerJoinAction
    : IGameAction<PokerJoinCommand, PokerExecutionState, JoinResult>
{
    public GameDecision<PokerExecutionState, JoinResult> Decide(
        GameActionInput<PokerExecutionState, PokerJoinCommand> input)
    {
        var command = input.Command;
        if (input.State.Table is not { } source || source.Status == PokerTableStatus.Closed
            || source.ChatId != command.ChatId)
            return Reject(input.State, PokerError.TableNotFound, command.MaxPlayers);
        if (source.Status is not (PokerTableStatus.Seating or PokerTableStatus.HandComplete))
            return Reject(input.State, PokerError.HandInProgress, command.MaxPlayers);
        if (input.State.Seats.Any(seat => seat.UserId == command.ActorUserId))
            return Reject(input.State, PokerError.AlreadySeated, command.MaxPlayers);
        if (input.State.ActorBalance < command.BuyIn)
            return Reject(input.State, PokerError.NotEnoughCoins, command.MaxPlayers);
        if (input.State.Seats.Count >= command.MaxPlayers)
            return Reject(input.State, PokerError.TableFull, command.MaxPlayers);

        var state = PokerExecutionRules.Clone(input.State);
        var used = state.Seats.Select(seat => seat.Position).ToHashSet();
        var position = 0;
        while (used.Contains(position)) position++;
        state.Seats.Add(new PokerSeat
        {
            InviteCode = source.InviteCode, Position = position, UserId = command.ActorUserId,
            DisplayName = command.DisplayName, Stack = command.BuyIn, ChatId = command.ChatId,
            JoinedAt = input.UtcNow.ToUnixTimeMilliseconds(),
        });
        return new(DecisionStatus.Accepted, state,
            new(PokerError.None, PokerExecutionRules.Snapshot(state), state.Seats.Count, command.MaxPlayers),
            [], [], [],
            [new PokerPlayerJoined(source.InviteCode, command.ActorUserId, position, command.BuyIn,
                input.UtcNow.ToUnixTimeMilliseconds())], [],
            CustomEffects:
            [WalletEconomyEffect.Debit(command.ActorUserId, command.ChatId, command.BuyIn, "poker.join")]);
    }

    private static GameDecision<PokerExecutionState, JoinResult> Reject(
        PokerExecutionState state, PokerError error, int maxPlayers) =>
        new(DecisionStatus.Rejected, state, new(error, null, 0, maxPlayers), [], [], [], [], [], error.ToString());
}

public sealed class PokerStartAction
    : IGameAction<PokerStartCommand, PokerExecutionState, StartResult>
{
    public GameDecision<PokerExecutionState, StartResult> Decide(
        GameActionInput<PokerExecutionState, PokerStartCommand> input)
    {
        if (input.State.Table is not { } table
            || input.State.Seats.All(seat => seat.UserId != input.Command.ActorUserId))
            return Reject(input.State, PokerError.NoTable);
        if (table.HostUserId != input.Command.ActorUserId) return Reject(input.State, PokerError.NotHost);
        if (table.Status == PokerTableStatus.HandActive) return Reject(input.State, PokerError.HandInProgress);
        if (input.State.Seats.Count(seat => seat.Stack > 0) < 2) return Reject(input.State, PokerError.NeedTwo);

        var state = PokerExecutionRules.Clone(input.State);
        var deck = Deck.BuildShuffled(PokerExecutionRules.ShuffleEntropyNames
            .Select(input.Entropy.GetDouble).ToArray());
        PokerDomain.StartHand(state.Table!, state.Seats, deck, input.UtcNow.ToUnixTimeMilliseconds());
        var active = state.Seats.Count(seat => seat.Status is PokerSeatStatus.Seated or PokerSeatStatus.AllIn);
        return new(DecisionStatus.Accepted, state,
            new(PokerError.None, PokerExecutionRules.Snapshot(state)), [], [], [],
            [new PokerHandStarted(table.InviteCode, active, input.UtcNow.ToUnixTimeMilliseconds())], []);
    }

    private static GameDecision<PokerExecutionState, StartResult> Reject(
        PokerExecutionState state, PokerError error) =>
        new(DecisionStatus.Rejected, state, new(error, null), [], [], [], [], [], error.ToString());
}

public sealed class PokerPlayerTurnAction
    : IGameAction<PokerPlayerTurnCommand, PokerExecutionState, ActionResult>
{
    public GameDecision<PokerExecutionState, ActionResult> Decide(
        GameActionInput<PokerExecutionState, PokerPlayerTurnCommand> input)
    {
        if (input.State.Table is not { Status: PokerTableStatus.HandActive } table)
            return Reject(input.State, PokerError.NotYourTurn);
        var state = PokerExecutionRules.Clone(input.State);
        var live = state.Seats.FirstOrDefault(seat => seat.UserId == input.Command.ActorUserId);
        if (live is null) return Reject(input.State, PokerError.NoTable);
        if (live.Position != table.CurrentSeat || live.Status != PokerSeatStatus.Seated)
            return Reject(input.State, PokerError.NotYourTurn);
        var action = PokerAction.FromVerb(input.Command.Verb, input.Command.Amount);
        if (action is null) return Reject(input.State, PokerError.InvalidAction);
        var validation = PokerDomain.Validate(state.Table!, live, action.Value);
        if (validation != ValidationResult.Ok)
            return Reject(input.State, PokerExecutionRules.MapValidation(validation));
        PokerDomain.Apply(state.Table!, live, action.Value);
        live.HasActedThisRound = true;
        state.Table!.LastActionAt = input.UtcNow.ToUnixTimeMilliseconds();
        var resolution = PokerExecutionRules.Resolve(state, input.UtcNow);
        return new(DecisionStatus.Accepted, state, resolution.Result, [], [], [], resolution.Events, [],
            CustomEffects: resolution.Effects);
    }

    internal static GameDecision<PokerExecutionState, ActionResult> Reject(
        PokerExecutionState state, PokerError error) =>
        new(DecisionStatus.Rejected, state,
            new(error, null, HandTransition.None, null, null, null), [], [], [], [], [], error.ToString());
}

public sealed class PokerAutoTurnAction
    : IGameAction<PokerAutoTurnCommand, PokerExecutionState, ActionResult>
{
    public GameDecision<PokerExecutionState, ActionResult> Decide(
        GameActionInput<PokerExecutionState, PokerAutoTurnCommand> input)
    {
        if (input.State.Table is not { Status: PokerTableStatus.HandActive })
            return PokerPlayerTurnAction.Reject(input.State, PokerError.NotYourTurn);
        var state = PokerExecutionRules.Clone(input.State);
        var current = state.Seats.FirstOrDefault(seat => seat.Position == state.Table!.CurrentSeat);
        if (current is not { Status: PokerSeatStatus.Seated })
            return PokerPlayerTurnAction.Reject(input.State, PokerError.NotYourTurn);
        var action = PokerDomain.DecideAutoAction(state.Table!, current);
        PokerDomain.Apply(state.Table!, current, action);
        current.HasActedThisRound = true;
        state.Table!.LastActionAt = input.UtcNow.ToUnixTimeMilliseconds();
        var autoKind = action.Kind == PokerActionKind.Check ? AutoAction.Check : AutoAction.Fold;
        var resolution = PokerExecutionRules.Resolve(state, input.UtcNow);
        return new(DecisionStatus.Accepted, state,
            resolution.Result with { AutoActorName = current.DisplayName, AutoKind = autoKind },
            [], [], [], resolution.Events, [], CustomEffects: resolution.Effects);
    }
}

public sealed class PokerLeaveAction
    : IGameAction<PokerLeaveCommand, PokerExecutionState, LeaveResult>
{
    public GameDecision<PokerExecutionState, LeaveResult> Decide(
        GameActionInput<PokerExecutionState, PokerLeaveCommand> input)
    {
        if (input.State.Table is null)
            return Reject(input.State, PokerError.NoTable);
        var state = PokerExecutionRules.Clone(input.State);
        var seat = state.Seats.FirstOrDefault(item => item.UserId == input.Command.ActorUserId);
        if (seat is null) return Reject(input.State, PokerError.NoTable);
        var effects = new List<IGameEffect>();
        var events = new List<IDomainEvent>();
        if (seat.Stack > 0)
            effects.Add(WalletEconomyEffect.Credit(seat.UserId, seat.ChatId, seat.Stack, "poker.leave"));

        if (state.Table!.Status == PokerTableStatus.HandActive && seat.Status == PokerSeatStatus.Seated)
        {
            seat.Status = PokerSeatStatus.Folded;
            seat.Stack = 0;
            var resolution = PokerExecutionRules.Resolve(state, input.UtcNow);
            effects.AddRange(resolution.Effects);
            events.AddRange(resolution.Events);
        }
        state.Seats.RemoveAll(item => item.UserId == input.Command.ActorUserId);
        var closed = state.Seats.Count == 0;
        if (closed) state.Table.Status = PokerTableStatus.Closed;
        var snapshot = closed ? null : PokerExecutionRules.Snapshot(state);
        return new(DecisionStatus.Accepted, state, new(PokerError.None, snapshot, closed),
            [], [], [], events, [], CustomEffects: effects);
    }

    private static GameDecision<PokerExecutionState, LeaveResult> Reject(
        PokerExecutionState state, PokerError error) =>
        new(DecisionStatus.Rejected, state, new(error, null, false), [], [], [], [], [], error.ToString());
}

public sealed class PokerSetMessageAction
    : IGameAction<PokerSetMessageCommand, PokerExecutionState, bool>
{
    public GameDecision<PokerExecutionState, bool> Decide(
        GameActionInput<PokerExecutionState, PokerSetMessageCommand> input)
    {
        if (input.State.Table is null)
            return new(DecisionStatus.Rejected, input.State, false, [], [], [], [], [], "no_table");
        var state = PokerExecutionRules.Clone(input.State);
        state.Table!.StateMessageId = input.Command.MessageId;
        return new(DecisionStatus.Accepted, state, true, [], [], [], [], []);
    }
}
