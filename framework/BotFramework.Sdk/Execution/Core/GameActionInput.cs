namespace BotFramework.Sdk.Execution;

public sealed record GameActionInput<TState, TCommand>(
    TCommand Command,
    TState State,
    WalletSnapshot Wallet,
    IReadOnlyDictionary<string, QuotaSnapshot> Quotas,
    EntropyValue Entropy,
    DateTimeOffset UtcNow);
