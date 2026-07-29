using BotFramework.Host.Contracts.Economics;

namespace BotFramework.Sdk.Economics;

/// <summary>
/// Pure wallet rules shared by the legacy economics service and wallet-owned adapters.
/// Persistence, locks, idempotency and responsible-gaming checks stay outside this policy.
/// </summary>
public static class WalletMutationPolicy
{
    public static WalletMutationDecision ApplyBatch(
        WalletMutationState state,
        IReadOnlyList<WalletBatchEffect> effects,
        bool allowNegative)
    {
        ArgumentNullException.ThrowIfNull(effects);

        var balance = state.Balance;
        var lines = new List<WalletMutationLine>(effects.Count);
        foreach (var effect in effects)
        {
            Validate(effect);
            var delta = effect.Kind switch
            {
                WalletBatchEffectKind.Debit => checked(-effect.Amount),
                WalletBatchEffectKind.Credit => effect.Amount,
                _ => throw new ArgumentOutOfRangeException(nameof(effects), effect.Kind, "Unknown wallet batch effect kind."),
            };
            var nextBalance = checked(balance + delta);
            if (!allowNegative && nextBalance < 0)
                return new(false, true, state.Balance, state.Version, []);

            balance = nextBalance;
            lines.Add(new WalletMutationLine(delta, balance, effect.Reason));
        }

        return new(true, false, balance, checked(state.Version + lines.Count), lines);
    }

    public static WalletMutationDecision ApplyDelta(
        WalletMutationState state,
        int delta,
        bool allowNegative,
        string reason)
    {
        if (delta == 0)
            return new(false, false, state.Balance, state.Version, []);
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Wallet mutation reason is required.", nameof(reason));

        var nextBalance = checked(state.Balance + delta);
        if (!allowNegative && nextBalance < 0)
            return new(false, true, state.Balance, state.Version, []);

        return new(
            true,
            false,
            nextBalance,
            checked(state.Version + 1),
            [new WalletMutationLine(delta, nextBalance, reason)]);
    }

    public static bool IsProtectedWager(string reason) =>
        !reason.StartsWith("admin.", StringComparison.Ordinal) &&
        !reason.StartsWith("transfer.", StringComparison.Ordinal) &&
        !reason.EndsWith(".rollback", StringComparison.Ordinal);

    private static void Validate(WalletBatchEffect effect)
    {
        if (effect.Amount <= 0 || string.IsNullOrWhiteSpace(effect.Reason))
            throw new ArgumentException("Wallet batch effects require a positive amount and reason.", nameof(effect));
    }
}
