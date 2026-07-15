namespace BotFramework.Sdk.Admin.Execution;

using BotFramework.Contracts.Tenancy;

/// <summary>A typed mutation requested by an admin action.</summary>
public interface IAdminEffect;

public sealed record AdminActor(long Id, string Name);

public sealed record AdminExecutionEnvelope(
    AdminActor Actor,
    string Action,
    object? AuditDetails = null)
{
    public TenantContext? TenantContext { get; init; }
}

public sealed record AdminEffectPlan<TResult>(
    TResult Result,
    IReadOnlyList<IAdminEffect> Effects,
    Func<IReadOnlyDictionary<string, object?>, TResult>? ResultFactory = null);
