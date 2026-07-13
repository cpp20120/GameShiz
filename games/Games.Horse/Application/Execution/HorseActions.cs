using BotFramework.Sdk.Execution;
using static Games.Horse.Domain.Rules.HorseResultHelpers;

namespace Games.Horse.Application.Execution;

public sealed class HorsePlaceBetAction
    : IGameAction<HorsePlaceBetCommand, HorseBetState, BetResult>
{
    public GameDecision<HorseBetState, BetResult> Decide(
        GameActionInput<HorseBetState, HorsePlaceBetCommand> input)
    {
        var command = input.Command;
        var balance = checked((int)input.Wallet.Balance);
        if (command.HorseId < 1 || command.HorseId > command.HorseCount)
            return Reject(input.State, BetFail(HorseError.InvalidHorseId), "invalid_horse");
        if (input.State.Bet is not null)
        {
            return Reject(input.State,
                new BetResult(HorseError.None, command.HorseId, command.Amount, balance),
                "duplicate_bet");
        }
        if (command.Amount <= 0 || command.Amount > input.Wallet.Balance)
            return Reject(input.State, BetFail(HorseError.InvalidAmount, command.HorseId, balance), "invalid_amount");

        var bet = new HorseBetRow(command.BetId, command.RaceDate, command.UserId,
            command.BalanceScopeId, command.HorseId - 1, command.Amount);
        return new(
            DecisionStatus.Accepted,
            new HorseBetState(bet),
            new BetResult(HorseError.None, command.HorseId, command.Amount, balance - command.Amount),
            [EconomyEffect.Debit(command.Amount, "horse.bet")], [], [],
            [new HorseBetPlaced(command.UserId, command.HorseId, command.Amount, command.RaceDate,
                input.UtcNow.ToUnixTimeMilliseconds())], []);
    }

    private static GameDecision<HorseBetState, BetResult> Reject(
        HorseBetState state, BetResult result, string reason) =>
        new(DecisionStatus.Rejected, state, result, [], [], [], [], [], reason);
}

public sealed class HorseRunAction
    : IGameAction<HorseRunCommand, HorseRaceState, RaceOutcome>
{
    public const string WinnerEntropy = "winner";

    public GameDecision<HorseRaceState, RaceOutcome> Decide(
        GameActionInput<HorseRaceState, HorseRunCommand> input)
    {
        var command = input.Command;
        if (!command.IsAdmin)
            return Reject(input.State, RaceFail(HorseError.NotAdmin), "not_admin");
        if (input.State.Winner is not null)
            return Reject(input.State, RaceFail(HorseError.NotEnoughBets), "already_completed");
        if (input.State.Bets.Count < command.MinBetsToRun)
            return Reject(input.State, RaceFail(HorseError.NotEnoughBets), "not_enough_bets");

        var stakes = Enumerable.Range(0, command.HorseCount).ToDictionary(index => index, _ => 0);
        foreach (var bet in input.State.Bets) stakes[bet.HorseId] += bet.Amount;
        var coefficients = HorseRules.GetCoefficients(stakes);
        var winner = Math.Min(command.HorseCount - 1,
            (int)(input.Entropy.GetDouble(WinnerEntropy) * command.HorseCount));
        var transactions = input.State.Bets
            .Where(bet => bet.HorseId == winner)
            .Select(bet => new RaceTransaction(bet.UserId, bet.BalanceScopeId,
                (int)Math.Floor(bet.Amount * coefficients[bet.HorseId])))
            .ToList();
        var payoutByWallet = transactions
            .GroupBy(transaction => (transaction.UserId, transaction.BalanceScopeId))
            .Select(group => new RaceTransaction(group.Key.UserId, group.Key.BalanceScopeId,
                group.Sum(transaction => transaction.Amount)))
            .ToList();
        var wonByUser = payoutByWallet.GroupBy(item => item.UserId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));
        var participants = input.State.Bets.GroupBy(bet => bet.UserId)
            .Select(group => new RacerSummary(group.Key, group.Sum(bet => bet.Amount),
                wonByUser.GetValueOrDefault(group.Key)))
            .ToList();
        var betScopeIds = input.State.Bets.Select(bet => bet.BalanceScopeId)
            .Where(scopeId => scopeId != 0).Distinct().ToList();
        var resultScopes = command.Kind == HorseRunKind.Global
            ? betScopeIds.Prepend(0L).Distinct().ToArray()
            : [command.ResultScopeId];
        var pot = input.State.Bets.Sum(bet => bet.Amount);

        return new(
            DecisionStatus.Accepted,
            new HorseRaceState(input.State.Bets, resultScopes, winner),
            new RaceOutcome(HorseError.None, winner, [], transactions, participants, betScopeIds,
                command.RaceDate),
            [], [], [],
            [new HorseRaceFinished(command.RaceDate, winner + 1, input.State.Bets.Count,
                transactions.Count, pot, input.UtcNow.ToUnixTimeMilliseconds())],
            [],
            CustomEffects: payoutByWallet
                .Where(item => item.Amount > 0)
                .Select(item => (IGameEffect)WalletEconomyEffect.Credit(
                    item.UserId, item.BalanceScopeId, item.Amount, "horse.payout"))
                .ToArray());
    }

    private static GameDecision<HorseRaceState, RaceOutcome> Reject(
        HorseRaceState state, RaceOutcome result, string reason) =>
        new(DecisionStatus.Rejected, state, result, [], [], [], [], [], reason);
}

public static class HorseRules
{
    public static Dictionary<int, double> GetCoefficients(IReadOnlyDictionary<int, int> stakes)
    {
        var sum = stakes.Values.Sum();
        return stakes.ToDictionary(
            pair => pair.Key,
            pair => pair.Value == 0
                ? 1.0
                : Math.Floor((sum - pair.Value) / (1.1 * pair.Value) * 1000) / 1000 + 1);
    }
}
