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
