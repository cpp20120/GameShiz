using BotFramework.Sdk.Admin.Execution;

namespace BotFramework.Sdk.Admin.Effects;

/// <summary>Atomically ensures a wallet and appends one administrative ledger mutation.</summary>
public sealed record WalletAdjustmentAdminEffect(
    long UserId,
    long BalanceScopeId,
    int Delta,
    string Reason,
    string? OperationId = null,
    string? DisplayName = null,
    bool AllowNegative = false) : IAdminEffect;

/// <summary>Atomically sets a wallet balance while deriving the ledger delta under the row lock.</summary>
public sealed record WalletSetAdminEffect(
    long UserId,
    long BalanceScopeId,
    int Balance,
    string Reason,
    string? OperationId = null,
    string? DisplayName = null,
    bool AllowNegative = false) : IAdminEffect;

/// <summary>Atomically compensates a ledger row and records the compensation.</summary>
public sealed record LedgerRevertAdminEffect(long LedgerId) : IAdminEffect;
