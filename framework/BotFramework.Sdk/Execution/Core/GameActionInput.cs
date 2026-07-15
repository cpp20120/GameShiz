namespace BotFramework.Sdk.Execution;

using BotFramework.Contracts.Economics;
using BotFramework.Contracts.Tenancy;

public sealed record GameActionInput<TState, TCommand>(
    TCommand Command,
    TState State,
    WalletSnapshot Wallet,
    IReadOnlyDictionary<string, QuotaSnapshot> Quotas,
    EntropyValue Entropy,
    DateTimeOffset UtcNow)
{
    /// <summary>Canonical tenant boundary for SDK 0.9 game decisions.</summary>
    public TenantContext? TenantContext { get; init; }

    /// <summary>Scope-local opaque wallet snapshot, when the request has a tenant context.</summary>
    public TenantWalletAccount? TenantWallet { get; init; }
}
