using CasinoShiz.Identity.Transport.Grpc;
using CasinoShiz.AdminBff.Pages;
using CasinoShiz.Wallet.Transport.Grpc;
using Games.Admin.Transport.Grpc;
using CasinoShiz.Operations.Transport.Grpc;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;
using CasinoShiz.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddRazorPages();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "casinoshiz.admin";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.IdleTimeout = TimeSpan.FromHours(8);
});

var backend = new Uri(builder.Configuration["Services:Backend:Address"] ?? "http://localhost:5081");
var wallet = new Uri(builder.Configuration["Services:Wallet:Address"] ?? backend.ToString());
var identity = new Uri(builder.Configuration["Services:Identity:Address"] ?? backend.ToString());
var operationsApiKey = builder.Configuration["Services:Operations:ApiKey"] ?? "";
builder.Services.AddReverseProxy()
    .LoadFromMemory(
    [
        new RouteConfig { RouteId = "legacy-admin-root", ClusterId = "backend", Match = new RouteMatch { Path = "/admin" } },
        new RouteConfig { RouteId = "legacy-admin", ClusterId = "backend", Match = new RouteMatch { Path = "/admin/{**catch-all}" } },
    ],
    [
        new ClusterConfig
        {
            ClusterId = "backend",
            Destinations = new Dictionary<string, DestinationConfig>
(StringComparer.Ordinal) {
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
builder.Services.AddAdminGrpcClients(backend);
builder.Services.AddWalletGrpcClients(wallet);
builder.Services.AddIdentityGrpcClient(identity);
builder.Services.AddOperationsGrpcClient(backend, builder.Configuration["Services:Operations:ApiKey"] ?? "");

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
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy", service = "casinoshiz-admin-bff" }));
await app.RunAsync();
