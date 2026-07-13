namespace BotFramework.Sdk.Execution;

/// <summary>
/// Targets an explicitly identified wallet. Intended for atomic multi-wallet commands such as
/// race payouts and transfers; every targeted wallet must also be declared as an executor lock.
/// </summary>
public sealed record WalletEconomyEffect(
    long UserId,
    long BalanceScopeId,
    EconomyEffectKind Kind,
    long Amount,
    string Reason) : IGameEffect
{
    public static WalletEconomyEffect Credit(long userId, long balanceScopeId, long amount, string reason) =>
        new(userId, balanceScopeId, EconomyEffectKind.Credit, RequirePositive(amount), reason);

    public static WalletEconomyEffect Debit(long userId, long balanceScopeId, long amount, string reason) =>
        new(userId, balanceScopeId, EconomyEffectKind.Debit, RequirePositive(amount), reason);

    private static long RequirePositive(long amount) => amount > 0
        ? amount
        : throw new ArgumentOutOfRangeException(nameof(amount), amount, "Effect amount must be positive.");
}
