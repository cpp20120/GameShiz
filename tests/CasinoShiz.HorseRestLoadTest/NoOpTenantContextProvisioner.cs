using BotFramework.Contracts.Tenancy;

namespace CasinoShiz.HorseRestLoadTest;

internal sealed class NoOpTenantContextProvisioner : ITenantContextProvisioner
{
    public Task EnsureAsync(TenantContext context, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
