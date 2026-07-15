using System.Net;
using System.Net.Http.Json;
using BotFramework.Client;
using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;
using BotFramework.Sdk.Commands;
using BotFramework.Sdk.Execution;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class FrameworkSdkContractTests
{
    [Fact]
    public async Task Client_RequiresIdempotencyForStateChangingCalls_AndCarriesTenantHeaders()
    {
        using var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        var client = new BotFrameworkClient(
            http,
            new BotFrameworkClientOptions(new Uri("https://api.example.test/")));
        var context = new BotFrameworkTenantContext(
            TenantId.Create("tenant-a"),
            ScopeId.Create("main"),
            PlayerId.Create("player-1"));

        await Assert.ThrowsAsync<ArgumentException>(() => client.SendAsync<object, object>(
            HttpMethod.Post,
            "coinflip",
            "flip",
            context,
            new { value = 1 }));

        await client.SendAsync<object, object>(
            HttpMethod.Post,
            "coinflip",
            "flip",
            context,
            new { value = 1 },
            "operation-1",
            "correlation-1");

        Assert.Equal(
            "https://api.example.test/api/v1/tenants/tenant-a/scopes/main/coinflip/flip",
            handler.Request!.RequestUri!.ToString());
        Assert.Equal("operation-1", handler.Request.Headers.GetValues("Idempotency-Key").Single());
        Assert.Equal("correlation-1", handler.Request.Headers.GetValues("X-Correlation-ID").Single());
        Assert.NotEmpty(handler.Request.Headers.GetValues("X-Request-ID").Single());
    }

    [Fact]
    public void TenantSdkFactories_PreserveOpaqueContext()
    {
        var context = TenantContext.Create(
            TenantId.Create("tenant-a"),
            ScopeId.Create("topic:1"),
            PlayerId.Create("player-a"),
            BotChannel.Telegram,
            RequestId.Create("request-1"),
            RequestId.Create("correlation-1"));
        var request = RequestContextFactory.FromTenantContext(context, "ru", "trace-1");

        Assert.Same(context, request.TenantContext);
        Assert.Equal(context, request.RequireTenantContext());
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { accepted = true }),
            });
        }
    }
}
