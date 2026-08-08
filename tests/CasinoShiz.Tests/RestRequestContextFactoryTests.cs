using System.Security.Claims;
using BotFramework.Rest;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class RestRequestContextFactoryTests
{
    [Fact]
    public void Create_WithoutBaggage_UsesSharedEmptyReadOnlyMap()
    {
        var factory = new RestRequestContextFactory(Options.Create(new RestFrameworkOptions()));

        var first = factory.Create(CreateContext());
        var second = factory.Create(CreateContext());

        Assert.Empty(first.Baggage);
        Assert.Same(first.Baggage, second.Baggage);
    }

    [Fact]
    public void Create_WithBaggage_PreservesCaseInsensitiveValues()
    {
        var context = CreateContext();
        context.Request.Headers["Baggage-Trace"] = "trace-value";
        context.Request.Headers["baggage-tenant"] = "tenant-value";

        var factory = new RestRequestContextFactory(Options.Create(new RestFrameworkOptions()));
        var request = factory.Create(context);

        Assert.Equal("trace-value", request.Baggage["baggage-trace"]);
        Assert.Equal("tenant-value", request.Baggage["BAGGAGE-TENANT"]);
        Assert.Equal(2, request.Baggage.Count);
    }

    [Fact]
    public void Create_ClaimValidation_PreservesTenantAndScopeSemantics()
    {
        var factory = new RestRequestContextFactory(Options.Create(new RestFrameworkOptions()));

        var request = factory.Create(CreateContext());

        Assert.Equal("tenant-a", request.Tenant.Value);
        Assert.Equal("scope-a", request.Scope.Value);
        Assert.Equal("player-1", request.Player.Value);
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues["tenantId"] = "tenant-a";
        context.Request.RouteValues["scopeId"] = "scope-a";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "player-1"),
            new Claim("tenant_id", "tenant-a"),
            new Claim("scope_id", "scope-a"),
            new Claim("name", "Player One"),
        ],
        "test"));
        return context;
    }
}
