using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class FrameworkTenancyTests
{
    [Fact]
    public void Opaque_ids_are_value_types_and_do_not_cross_tenant_boundaries()
    {
        var player = PlayerId.Create("same-player");
        var scope = ScopeId.Create("main");
        var a = TenantContext.Create(
            TenantId.Create("tenant-a"), scope, player, BotChannel.Rest, RequestId.New(), RequestId.New());
        var b = TenantContext.Create(
            TenantId.Create("tenant-b"), scope, player, BotChannel.Rest, RequestId.New(), RequestId.New());

        var wallets = new Dictionary<(TenantId Tenant, ScopeId Scope, PlayerId Player), int>
        {
            [(a.TenantId, a.ScopeId, a.PlayerId!.Value)] = 10,
            [(b.TenantId, b.ScopeId, b.PlayerId!.Value)] = 20,
        };

        Assert.Equal(10, wallets[(a.TenantId, a.ScopeId, a.PlayerId!.Value)]);
        Assert.Equal(20, wallets[(b.TenantId, b.ScopeId, b.PlayerId!.Value)]);
        Assert.NotEqual(a.TenantId, b.TenantId);
    }

    [Fact]
    public void Tenant_context_accessor_is_scoped_and_restores_previous_context()
    {
        var accessor = new TenantContextAccessor();
        var first = TenantContext.Create(
            TenantId.Create("tenant-a"), ScopeId.Create("main"), null, BotChannel.Rest, RequestId.New(), RequestId.New());
        var second = first with { TenantId = TenantId.Create("tenant-b") };

        using (accessor.Push(first))
        {
            Assert.Equal(first, accessor.Current);
            using (accessor.Push(second))
                Assert.Equal(second, accessor.Current);
            Assert.Equal(first, accessor.Current);
        }

        Assert.Null(accessor.Current);
    }
}
