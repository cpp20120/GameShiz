using BotFramework.Contracts.Tenancy;
using BotFramework.Sdk.Admin.Execution;

namespace BotFramework.Sdk.Admin.Effects;

public sealed record TenantWalletAdjustmentAdminEffect(
    TenantId TenantId,
    ScopeId ScopeId,
    PlayerId PlayerId,
    int Delta,
    string Reason,
    string? OperationId = null,
    string? DisplayName = null,
    bool AllowNegative = false) : IAdminEffect;