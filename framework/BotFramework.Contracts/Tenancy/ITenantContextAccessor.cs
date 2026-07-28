namespace BotFramework.Contracts.Tenancy;

/// <summary>Access to the current request's tenant boundary.</summary>
public interface ITenantContextAccessor
{
    TenantContext? Current { get; }

    TenantContext RequireCurrent() =>
        Current ?? throw new InvalidOperationException("Tenant context is unavailable outside an inbound operation.");

    IDisposable Push(TenantContext context);
}
