using BotFramework.Sdk.Admin.Execution;

namespace BotFramework.Sdk.Admin.Effects;

/// <summary>Atomically sets a wallet balance while deriving the ledger delta under the row lock.</summary>
public sealed record WalletSetAdminEffect(
    long UserId,
    long BalanceScopeId,
    int Balance,
    string Reason,
    string? OperationId = null,
    string? DisplayName = null,
    bool AllowNegative = false) : IAdminEffect;