using CasinoShiz.Identity.Transport.Grpc;
using CasinoShiz.AdminBff.Pages;
using CasinoShiz.Wallet.Transport.Grpc;
using Games.Admin.Transport.Grpc;
using CasinoShiz.Operations.Transport.Grpc;
using CasinoShiz.ServiceDefaults;
using BotFramework.Contracts.Identity;
using BotFramework.Contracts.Operations;
using BotFramework.Host.Contracts.Economics;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddRazorPages();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = builder.Configuration["Admin:CookieName"] ?? "casinoshiz.admin";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.IdleTimeout = TimeSpan.FromHours(8);
});

var backend = new Uri(builder.Configuration["Services:Backend:Address"] ?? "http://localhost:5081");
var backendGrpc = new Uri(builder.Configuration["Services:Backend:GrpcAddress"] ?? backend.ToString());
var wallet = new Uri(builder.Configuration["Services:Wallet:Address"] ?? backend.ToString());
var identity = new Uri(builder.Configuration["Services:Identity:Address"] ?? backend.ToString());
var operationsApiKey = builder.Configuration["Services:Operations:ApiKey"] ?? "";
var proxiedAdminPaths = new[]
{
    "/admin/bets",
    "/admin/challenges",
    "/admin/events",
    "/admin/groups",
    "/admin/history",
    "/admin/horse",
    "/admin/horse/image",
    "/admin/meta",
    "/admin/meta-alerts",
    "/admin/meta-events",
    "/admin/meta-quests",
    "/admin/meta-seasons",
    "/admin/meta-tournament/{id}",
    "/admin/meta-tournaments",
    "/admin/quartz",
    "/admin/settings",
};
builder.Services.AddReverseProxy()
    .LoadFromMemory(
        proxiedAdminPaths.Select((path, index) => new RouteConfig
        {
            RouteId = $"backend-admin-{index}",
            ClusterId = "backend",
            Match = new RouteMatch { Path = path },
        }).ToArray(),
        [
            new ClusterConfig
            {
                ClusterId = "backend",
                Destinations = new Dictionary<string, DestinationConfig>(StringComparer.Ordinal)
                {
                    ["primary"] = new() { Address = backend.ToString() },
                },
            },
        ])
    .AddTransforms(transforms => transforms.AddRequestTransform(context =>
    {
        var session = context.HttpContext.Session;
        context.ProxyRequest.Headers.TryAddWithoutValidation("x-admin-api-key", operationsApiKey);
        context.ProxyRequest.Headers.TryAddWithoutValidation("x-admin-actor-id", session.ActorId().ToString(System.Globalization.CultureInfo.InvariantCulture));
        context.ProxyRequest.Headers.TryAddWithoutValidation("x-admin-actor-name", session.ActorName());
        context.ProxyRequest.Headers.TryAddWithoutValidation("x-admin-role", session.ActorRole());
        return ValueTask.CompletedTask;
    }));
builder.Services.AddAdminGrpcClients(backendGrpc);
builder.Services.AddWalletGrpcClients(wallet);
builder.Services.AddIdentityGrpcClient(identity);
builder.Services.AddOperationsGrpcClient(backendGrpc, builder.Configuration["Services:Operations:ApiKey"] ?? "");

var app = builder.Build();
app.UseStaticFiles();
app.UseSession();
app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/admin", StringComparison.Ordinal)
        || context.Request.Path.StartsWithSegments("/admin/login", StringComparison.Ordinal)
        || context.Request.Path.StartsWithSegments("/admin/logout", StringComparison.Ordinal))
    {
        await next();
        return;
    }

    if (!context.Session.IsAuthenticated())
    {
        context.Response.Redirect("/admin/login");
        return;
    }

    await next();
});
app.MapRazorPages();
app.MapReverseProxy();
app.MapGet("/", () => Results.Redirect("/admin"));
app.MapGet("/api/aggregation/players/{userId:long}", async (
    long userId,
    long? balanceScopeId,
    IPlayerDirectory identities,
    IWalletReadService wallets,
    CancellationToken ct) =>
{
    var identityTask = identities.GetAsync(userId, ct);
    var walletTask = balanceScopeId is { } scope
        ? wallets.GetAsync(userId, scope, ct)
        : Task.FromResult<WalletAccount?>(null);
    await Task.WhenAll(identityTask, walletTask);

    var identityResult = await identityTask;
    var walletResult = await walletTask;
    return identityResult is null && walletResult is null
        ? Results.NotFound()
        : Results.Ok(new PlayerAggregateResponse(identityResult, walletResult));
});
app.MapGet("/api/aggregation/admin", async (
    IOperationsAdminService operations,
    IWalletAnalyticsService walletAnalytics,
    CancellationToken ct) =>
{
    var failuresTask = operations.ListFailuresAsync(25, null, ct);
    var outboxTask = operations.ListOutboxAsync(25, null, ct);
    var jobsTask = operations.ListJobsAsync(ct);
    var walletHealthTask = walletAnalytics.GetHealthAsync(ct);
    await Task.WhenAll(failuresTask, outboxTask, jobsTask, walletHealthTask);

    return Results.Ok(new AdminAggregateResponse(
        await failuresTask,
        await outboxTask,
        await jobsTask,
        await walletHealthTask));
});
app.MapServiceDefaults();
await app.RunAsync();

internal sealed record PlayerAggregateResponse(PlayerIdentity? Identity, WalletAccount? Wallet);
internal sealed record AdminAggregateResponse(
    IReadOnlyList<OperationFailure> Failures,
    IReadOnlyList<OperationOutbox> Outbox,
    IReadOnlyList<OperationJob> Jobs,
    WalletHealth WalletHealth);
