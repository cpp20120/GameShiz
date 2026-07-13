namespace BotFramework.Sdk.Execution;

public sealed record EconomyEffect(EconomyEffectKind Kind, long Amount, string Reason) : IGameEffect
{
    public static EconomyEffect Debit(long amount, string reason) =>
        new(EconomyEffectKind.Debit, RequirePositive(amount), reason);

    public static EconomyEffect Credit(long amount, string reason) =>
        new(EconomyEffectKind.Credit, RequirePositive(amount), reason);

    private static long RequirePositive(long amount) => amount > 0
        ? amount
        : throw new ArgumentOutOfRangeException(nameof(amount), amount, "Effect amount must be positive.");
}
