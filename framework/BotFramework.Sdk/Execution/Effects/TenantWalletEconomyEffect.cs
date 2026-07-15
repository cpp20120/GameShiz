using BotFramework.Contracts.Tenancy;

namespace BotFramework.Sdk.Execution;

/// <summary>
/// SDK 0.9 wallet effect. The wallet is scope-local and addressed only by
/// opaque tenant/scope/player identifiers; numeric platform ids stay inside
/// channel adapters and Host persistence.
/// </summary>
public sealed record TenantWalletEconomyEffect(
    TenantId TenantId,
    ScopeId ScopeId,
    PlayerId PlayerId,
    EconomyEffectKind Kind,
    long Amount,
    string Reason) : IGameEffect, IAtomicEffect
{
    public static TenantWalletEconomyEffect Credit(
        TenantContext context,
        long amount,
        string reason) =>
        Create(context, EconomyEffectKind.Credit, amount, reason);

    public static TenantWalletEconomyEffect Debit(
        TenantContext context,
        long amount,
        string reason) =>
        Create(context, EconomyEffectKind.Debit, amount, reason);

    private static TenantWalletEconomyEffect Create(
        TenantContext context,
        EconomyEffectKind kind,
        long amount,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.PlayerId is not { } player)
            throw new InvalidOperationException("A player is required for a wallet effect.");
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Effect amount must be positive.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Effect reason is required.", nameof(reason));

        return new(context.TenantId, context.ScopeId, player, kind, amount, reason);
    }
}
