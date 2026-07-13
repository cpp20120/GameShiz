using System.Security.Cryptography;
using System.Text;
using BotFramework.Sdk.Execution;
using Games.Pick.Domain.Events;

namespace Games.Pick.Application.Execution;

public sealed class PickAction : IGameAction<PickCommand, PickGameState, PickResult>
{
    public const string OutcomeEntropy = "outcome";

    public GameDecision<PickGameState, PickResult> Decide(
        GameActionInput<PickGameState, PickCommand> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var command = input.Command;
        if (Validate(command) is { } error)
            return Reject(input.State, Fail(error, command), error.ToString());
        if (command.Amount > input.Wallet.Balance)
            return Reject(input.State, BalanceFail(command, checked((int)input.Wallet.Balance)), "insufficient_balance");

        var pickedIndex = Math.Min(
            command.Variants.Count - 1,
            (int)(input.Entropy.GetDouble(OutcomeEntropy) * command.Variants.Count));
        var won = command.BackedIndices.Contains(pickedIndex);
        var streakBefore = input.State.Streak;
        var streakAfter = streakBefore;
        var streakBonus = 0;
        var payout = 0;

        if (won)
        {
            if (command.ApplyStreak)
            {
                streakAfter = checked(streakBefore + 1);
                streakBonus = ComputeStreakBonus(command, streakAfter);
            }
            payout = checked(ComputePayout(command) + streakBonus);
        }
        else if (command.ApplyStreak)
        {
            streakAfter = 0;
        }

        Guid? chainGuid = null;
        var custom = new List<IGameEffect>();
        if (won && command.ChainMaxDepth > 0 && command.Depth < command.ChainMaxDepth
            && payout > 0 && (command.MaxBet <= 0 || payout <= command.MaxBet))
        {
            chainGuid = DeterministicGuid(command.CommandId);
            custom.Add(new PickChainOfferEffect(new PickChainState(
                chainGuid.Value,
                command.UserId,
                command.ChatId,
                command.DisplayName,
                payout,
                command.Depth + 1,
                command.Variants,
                command.BackedIndices,
                input.UtcNow.AddSeconds(Math.Max(5, command.ChainTtlSeconds)))));
        }

        var balance = checked((int)(input.Wallet.Balance - command.Amount + payout));
        var result = new PickResult(
            PickError.None, command.Amount, balance, payout, payout - command.Amount,
            streakBonus, streakBefore, streakAfter, pickedIndex, won, command.Depth,
            chainGuid, command.Variants, command.BackedIndices);
        var economy = payout > 0
            ? new[]
            {
                EconomyEffect.Debit(command.Amount, command.Depth == 0 ? "pick.bet" : "pick.chain.bet"),
                EconomyEffect.Credit(payout, command.Depth == 0 ? "pick.win" : "pick.chain.win"),
            }
            : [EconomyEffect.Debit(command.Amount, command.Depth == 0 ? "pick.bet" : "pick.chain.bet")];
        var occurredAt = input.UtcNow.ToUnixTimeMilliseconds();
        return new(
            DecisionStatus.Accepted,
            new PickGameState(streakAfter),
            result,
            economy,
            [],
            [],
            [
                new PickPlayed(
                    command.UserId, command.ChatId, command.Amount, command.Variants.Count,
                    command.BackedIndices.Count, pickedIndex, won, payout, streakAfter,
                    command.Depth, occurredAt),
                new GameCompletedMetaEvent(
                    command.ChatId, command.UserId, command.DisplayName, "pick",
                    command.Amount, payout, payout > command.Amount,
                    decimal.Divide(payout, command.Amount), occurredAt),
            ],
            [],
            CustomEffects: custom);
    }

    private static PickError? Validate(PickCommand command)
    {
        if (command.Variants.Count < command.MinVariants) return PickError.NotEnoughVariants;
        if (command.Variants.Count > command.MaxVariants) return PickError.TooManyVariants;
        if (command.Amount <= 0 || command.MaxBet > 0 && command.Amount > command.MaxBet) return PickError.InvalidAmount;
        if (command.BackedIndices.Count == 0 || command.BackedIndices.Count >= command.Variants.Count) return PickError.InvalidChoice;
        return command.BackedIndices.Any(index => index < 0 || index >= command.Variants.Count)
            ? PickError.InvalidChoice
            : null;
    }

    private static int ComputePayout(PickCommand command)
    {
        var gross = (double)command.Amount * command.Variants.Count / Math.Max(1, command.BackedIndices.Count);
        return (int)Math.Max(0, Math.Floor(gross * (1.0 - Math.Clamp(command.HouseEdge, 0.0, 1.0))));
    }

    private static int ComputeStreakBonus(PickCommand command, int streakAfter)
    {
        var factor = Math.Min(Math.Max(0, streakAfter - 1), Math.Max(0, command.StreakCap));
        return (int)Math.Max(0, Math.Floor(command.Amount * factor * command.StreakBonusPerWin));
    }

    private static Guid DeterministicGuid(string commandId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(commandId));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static PickResult Fail(PickError error, PickCommand command) =>
        new(error, command.Amount, 0, 0, 0, 0, 0, 0, -1, false, command.Depth, null,
            command.Variants, command.BackedIndices);

    private static PickResult BalanceFail(PickCommand command, int balance) =>
        new(PickError.NotEnoughCoins, command.Amount, balance, 0, 0, 0, 0, 0, -1, false,
            command.Depth, null, command.Variants, command.BackedIndices);

    private static GameDecision<PickGameState, PickResult> Reject(
        PickGameState state, PickResult result, string reason) =>
        new(DecisionStatus.Rejected, state, result, [], [], [], [], [], reason);
}
