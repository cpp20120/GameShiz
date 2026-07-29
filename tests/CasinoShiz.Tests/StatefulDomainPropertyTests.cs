using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Games.Poker.Domain.Commands;
using Games.Poker.Domain.Entities;
using Games.Poker.Domain.Results;
using Games.Poker.Domain.Rules;
using Games.SecretHitler.Domain.Entities;
using Games.SecretHitler.Domain.Results;
using Games.SecretHitler.Domain.Rules;
using PokerDeck = Games.Poker.Domain.Rules.Deck;

namespace CasinoShiz.Tests;

public sealed class StatefulDomainPropertyTests
{
    [Property(MaxTest = 150)]
    public Property Poker_CommandSequence_PreservesChipAndCardInvariants(NonEmptyArray<int> commands)
    {
        var table = new PokerTable
        {
            InviteCode = "PBT",
            SmallBlind = 5,
            BigBlind = 10,
            Status = PokerTableStatus.Seating,
            Phase = PokerPhase.None,
        };
        var seats = Enumerable.Range(0, 3)
            .Select(position => new PokerSeat
            {
                InviteCode = "PBT",
                Position = position,
                UserId = position + 1,
                Stack = 100,
                Status = PokerSeatStatus.Seated,
            })
            .ToList();
        const int initialChips = 300;

        PokerDomain.StartHand(
            table,
            seats,
            PokerDeck.BuildShuffled(Enumerable.Repeat(0.37, 51).ToArray()),
            now: 1);

        var failure = CheckPokerInvariants(table, seats, initialChips);
        if (failure is null)
        {
            foreach (var rawCommand in commands.Get)
            {
                if (table.Status != PokerTableStatus.HandActive)
                    break;

                var magnitude = Math.Abs((long)rawCommand);
                var current = seats.SingleOrDefault(seat =>
                    seat.Position == table.CurrentSeat && seat.Status == PokerSeatStatus.Seated);
                if (current is null)
                    break;

                var action = CreatePokerAction(table, current, magnitude);
                if (PokerDomain.Validate(table, current, action) == ValidationResult.Ok)
                {
                    PokerDomain.Apply(table, current, action);
                    current.HasActedThisRound = true;
                    PokerDomain.ResolveAfterAction(table, seats);
                }

                failure = CheckPokerInvariants(table, seats, initialChips);
                if (failure is not null)
                    break;
            }
        }

        return (failure is null)
            .ToProperty()
            .Label(failure ?? $"commands={commands.Get.Length}, status={table.Status}, phase={table.Phase}");
    }

    [Property(MaxTest = 150)]
    public Property SecretHitler_CommandSequence_PreservesPolicyAndPhaseInvariants(NonEmptyArray<int> commands)
    {
        var game = new SecretHitlerGame
        {
            InviteCode = "PBT",
            HostUserId = 100,
            ChatId = 100,
            Status = ShStatus.Lobby,
        };
        var players = Enumerable.Range(0, 5)
            .Select(position => new SecretHitlerPlayer
            {
                InviteCode = "PBT",
                Position = position,
                UserId = 100 + position,
                IsAlive = true,
                Role = ShRole.Liberal,
            })
            .ToList();
        var roleEntropy = Enumerable.Repeat(0.37, 4).ToArray();
        var deckEntropy = Enumerable.Repeat(0.37, 16).ToArray();
        var reshuffleEntropy = Enumerable.Repeat(0.37, 16).ToArray();

        ShTransitions.StartGame(game, players, roleEntropy, deckEntropy);

        var failure = CheckSecretHitlerInvariants(game, players);
        if (failure is null)
        {
            foreach (var rawCommand in commands.Get)
            {
                if (game.Phase == ShPhase.GameEnd)
                    break;

                var magnitude = Math.Abs((long)rawCommand);
                ApplySecretHitlerCommand(game, players, magnitude, reshuffleEntropy);

                failure = CheckSecretHitlerInvariants(game, players);
                if (failure is not null)
                    break;
            }
        }

        return (failure is null)
            .ToProperty()
            .Label(failure ?? $"commands={commands.Get.Length}, status={game.Status}, phase={game.Phase}");
    }

    private static PokerAction CreatePokerAction(PokerTable table, PokerSeat seat, long magnitude)
    {
        return (magnitude % 5) switch
        {
            0 => table.CurrentBet > seat.CurrentBet
                ? new PokerAction(PokerActionKind.Call)
                : PokerAction.Check(),
            1 => PokerAction.Fold(),
            2 => new PokerAction(PokerActionKind.AllIn),
            3 => CreateRaiseOrCall(table, seat, magnitude),
            _ => new PokerAction(PokerActionKind.Raise, int.MaxValue),
        };
    }

    private static PokerAction CreateRaiseOrCall(PokerTable table, PokerSeat seat, long magnitude)
    {
        var minTotal = table.CurrentBet + Math.Max(table.BigBlind, table.MinRaise);
        var maxTotal = seat.CurrentBet + seat.Stack;
        if (minTotal > maxTotal)
            return new PokerAction(PokerActionKind.Call);

        var range = (long)maxTotal - minTotal + 1;
        var target = minTotal + (int)((magnitude / 5) % range);
        return new PokerAction(PokerActionKind.Raise, target);
    }

    private static string? CheckPokerInvariants(
        PokerTable table,
        IReadOnlyList<PokerSeat> seats,
        int initialChips)
    {
        if (seats.Any(seat => seat.Stack < 0 || seat.CurrentBet < 0 || seat.TotalCommitted < 0))
            return "poker contains a negative stack or bet";
        if (table.Pot < 0 || table.CurrentBet < 0 || table.MinRaise < 0)
            return "poker contains a negative table amount";

        var chips = seats.Sum(seat => seat.Stack) + table.Pot;
        if (chips != initialChips)
            return $"poker chips changed: expected={initialChips}, actual={chips}";

        if (table.Status == PokerTableStatus.HandComplete &&
            (table.Pot != 0 || seats.Sum(seat => seat.Stack) != initialChips))
            return "completed poker hand did not settle the pot";

        if (table.Status == PokerTableStatus.HandActive)
        {
            var visibleCards = PokerDeck.Parse(table.CommunityCards)
                .Concat(seats.SelectMany(seat => PokerDeck.Parse(seat.HoleCards)))
                .ToArray();
            var remainingCards = PokerDeck.Parse(table.DeckState);
            var allCards = visibleCards.Concat(remainingCards).ToArray();
            var distinctCards = allCards.Distinct(StringComparer.Ordinal).Count();
            var burnedCards = table.Phase switch
            {
                PokerPhase.PreFlop => 0,
                PokerPhase.Flop => 1,
                PokerPhase.Turn => 2,
                PokerPhase.River => 3,
                _ => 0,
            };
            var expectedCards = 52 - burnedCards;
            if (allCards.Length != expectedCards || distinctCards != expectedCards)
                return $"poker deck contains duplicate or missing cards: expected={expectedCards}, total={allCards.Length}, distinct={distinctCards}, visible={visibleCards.Length}, remaining={remainingCards.Length}, phase={table.Phase}";
        }

        return null;
    }

    private static void ApplySecretHitlerCommand(
        SecretHitlerGame game,
        IReadOnlyList<SecretHitlerPlayer> players,
        long magnitude,
        IReadOnlyList<double> reshuffleEntropy)
    {
        switch (game.Phase)
        {
            case ShPhase.Nomination:
            {
                var president = players.Single(player => player.Position == game.CurrentPresidentPosition);
                var candidate = (int)(magnitude % players.Count);
                if (magnitude % 5 != 0)
                {
                    for (var offset = 0; offset < players.Count; offset++)
                    {
                        var candidatePosition = (candidate + offset) % players.Count;
                        if (ShTransitions.ValidateNomination(game, president, candidatePosition, players) == ShValidation.Ok)
                        {
                            candidate = candidatePosition;
                            break;
                        }
                    }
                }

                if (ShTransitions.ValidateNomination(game, president, candidate, players) == ShValidation.Ok)
                    ShTransitions.ApplyNomination(game, candidate, players);
                break;
            }
            case ShPhase.Election:
            {
                var voter = magnitude % 4 == 0
                    ? players[(int)(magnitude % players.Count)]
                    : players.First(player => player.IsAlive && player.LastVote == ShVote.None);
                if (ShTransitions.ValidateVote(game, voter) == ShValidation.Ok)
                {
                    var vote = magnitude % 2 == 0 ? ShVote.Ja : ShVote.Nein;
                    ShTransitions.ApplyVote(game, voter, vote, players, reshuffleEntropy);
                }
                break;
            }
            case ShPhase.LegislativePresident:
            {
                var president = players.Single(player => player.Position == game.CurrentPresidentPosition);
                var discardIndex = magnitude % 5 == 0 ? 3 : (int)(magnitude % 3);
                if (ShTransitions.ValidatePresidentDiscard(game, president, discardIndex) == ShValidation.Ok)
                    ShTransitions.ApplyPresidentDiscard(game, discardIndex);
                break;
            }
            case ShPhase.LegislativeChancellor:
            {
                var chancellor = players.Single(player => player.Position == game.NominatedChancellorPosition);
                var enactIndex = magnitude % 5 == 0 ? 2 : (int)(magnitude % 2);
                if (ShTransitions.ValidateChancellorEnact(game, chancellor, enactIndex) == ShValidation.Ok)
                    ShTransitions.ApplyChancellorEnact(game, enactIndex, players);
                break;
            }
        }
    }

    private static string? CheckSecretHitlerInvariants(
        SecretHitlerGame game,
        IReadOnlyList<SecretHitlerPlayer> players)
    {
        var policyStateLength = game.DeckState.Length
            + game.DiscardState.Length
            + game.PresidentDraw.Length
            + game.ChancellorReceived.Length
            + game.LiberalPolicies
            + game.FascistPolicies;
        if (policyStateLength != 17)
            return $"secret hitler policy conservation failed: {policyStateLength}";

        if (game.LiberalPolicies is < 0 or > ShTransitions.LiberalWinThreshold ||
            game.FascistPolicies is < 0 or > ShTransitions.FascistWinThreshold)
            return "secret hitler policy count is out of range";
        if (game.ElectionTracker is < 0 or >= ShTransitions.ElectionTrackerCap)
            return "secret hitler election tracker is out of range";
        if (players.Count(player => player.Role == ShRole.Hitler) != 1)
            return "secret hitler has an invalid Hitler count";
        if (players.Any(player => player.LastVote is < ShVote.None or > ShVote.Nein))
            return "secret hitler contains an invalid vote";

        switch (game.Phase)
        {
            case ShPhase.Nomination:
                if (game.Status != ShStatus.Active || game.PresidentDraw.Length != 0 || game.ChancellorReceived.Length != 0)
                    return "nomination phase has inconsistent state";
                break;
            case ShPhase.Election:
                if (game.Status != ShStatus.Active || game.NominatedChancellorPosition < 0 ||
                    game.PresidentDraw.Length != 0 || game.ChancellorReceived.Length != 0)
                    return "election phase has inconsistent state";
                break;
            case ShPhase.LegislativePresident:
                if (game.Status != ShStatus.Active || game.PresidentDraw.Length != 3 || game.ChancellorReceived.Length != 0)
                    return "president phase has inconsistent state";
                break;
            case ShPhase.LegislativeChancellor:
                if (game.Status != ShStatus.Active || game.PresidentDraw.Length != 0 || game.ChancellorReceived.Length != 2)
                    return "chancellor phase has inconsistent state";
                break;
            case ShPhase.GameEnd:
                if (game.Status != ShStatus.Completed || game.Winner == ShWinner.None)
                    return "game-end phase has inconsistent state";
                break;
        }

        return null;
    }
}
