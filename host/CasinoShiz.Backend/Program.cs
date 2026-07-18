using Games.PixelBattle.Transport.Grpc;
using BotFramework.Host.Composition.Builder;
using Games.Dice.Infrastructure.Modules;
using Games.Dice.Transport.Grpc;
using Games.NativeDice.Transport.Grpc;
using Games.DiceCube.Infrastructure.Modules;
using Games.Darts.Infrastructure.Modules;
using Games.Football.Infrastructure.Modules;
using Games.Basketball.Infrastructure.Modules;
using Games.Bowling.Infrastructure.Modules;
using Games.Transfer.Infrastructure.Modules;
using Games.Transfer.Transport.Grpc;
using Games.Redeem.Infrastructure.Modules;
using Games.Redeem.Transport.Grpc;
using Games.Leaderboard.Infrastructure.Modules;
using Games.Leaderboard.Transport.Grpc;
using Games.PixelBattle.Infrastructure.Modules;
using Games.PixelBattle.Infrastructure.Integrations;
using Games.Pick.Infrastructure.Modules;
using Games.Pick.Transport.Grpc;
using Games.Blackjack.Infrastructure.Modules;
using Games.Blackjack.Transport.Grpc;
using Games.Horse.Infrastructure.Modules;
using Games.Horse.Application.Jobs;
using Games.Horse.Transport.Grpc;
using Games.Challenges.Infrastructure.Modules;
using Games.Challenges.Transport.Grpc;
using Games.Poker.Infrastructure.Modules;
using Games.Poker.Transport.Grpc;
using Games.SecretHitler.Infrastructure.Modules;
using Games.SecretHitler.Transport.Grpc;
using Games.Meta.Infrastructure.Modules;
using Games.Meta.Transport.Grpc;
using Games.Admin.Infrastructure.Modules;
using Games.Admin.Transport.Grpc;
using CasinoShiz.Backend;
using CasinoShiz.Identity;
using CasinoShiz.Identity.Transport.Grpc;
using CasinoShiz.Wallet.Transport.Grpc;
using CasinoShiz.Operations.Transport.Grpc;
using BotFramework.Host.Admin.Auth;
using BotFramework.Sdk.MiniGames;
using CasinoShiz.Host.Infrastructure;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Security.Cryptography;
using System.Text;
using CasinoShiz.Host.Pages.Admin;
using CasinoShiz.ServiceDefaults;
using BotFramework.Rendering;
using Games.Meta.Application.Tournaments;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080, listen => listen.Protocols = HttpProtocols.Http1);
    options.ListenAnyIP(8081, listen => listen.Protocols = HttpProtocols.Http2);
});
builder.AddServiceDefaults();
builder.Services.AddSingleton<HorseGifCache>();
builder.Services.AddScoped<IMiniGameSessionGhostHeal, MiniGameSessionGhostHeal>();

var selectedModules = BackendModuleComposition.Resolve(builder.Configuration);
if (selectedModules.Contains("meta"))
    builder.AddDurableWorkflows(typeof(TournamentWorkflowHandler).Assembly);
var framework = builder.AddBackendFramework()
    .AddSelectedModules(selectedModules);
if (selectedModules.Contains("horse"))
    builder.Services.AddHostedService<HorseLegacyScheduleCleanup>();

var walletRemote = string.Equals(builder.Configuration["Services:Wallet:Mode"], "Grpc", StringComparison.OrdinalIgnoreCase);
var identityRemote = string.Equals(builder.Configuration["Services:Identity:Mode"], "Grpc", StringComparison.OrdinalIgnoreCase);
if (!identityRemote)
    framework.AddModule<IdentityModule>();
if (walletRemote)
    builder.Services.AddWalletGrpcClients(new Uri(builder.Configuration["Services:Wallet:Address"]
        ?? throw new InvalidOperationException("Services:Wallet:Address is required in Grpc mode.")));
if (identityRemote)
    builder.Services.AddIdentityGrpcClient(new Uri(builder.Configuration["Services:Identity:Address"]
        ?? throw new InvalidOperationException("Services:Identity:Address is required in Grpc mode.")));
builder.Services.AddGrpc(options => options.Interceptors.Add<BotFramework.Host.Games.GameAvailabilityGrpcInterceptor>());

var app = builder.Build();

app.UseTransportChannelContext();

app.UseStaticFiles();
app.UseSession();
app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/admin", StringComparison.Ordinal))
    {
        await next();
        return;
    }

    var expected = app.Configuration["Services:Operations:ApiKey"];
    var supplied = context.Request.Headers["x-admin-api-key"].ToString();
    if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(supplied)
        || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied)))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    _ = long.TryParse(context.Request.Headers["x-admin-actor-id"], System.Globalization.CultureInfo.InvariantCulture, out var actorId);
    var actorName = context.Request.Headers["x-admin-actor-name"].ToString();
    var role = string.Equals(context.Request.Headers["x-admin-role"], "SuperAdmin", StringComparison.Ordinal)
        ? AdminRole.SuperAdmin : AdminRole.ReadOnly;
    context.Session.SetAdminSession(new AdminSession(actorId, actorName, role));
    await next();
});
app.MapSelectedTransports(selectedModules);
app.MapRenderHistory();
app.MapRazorPages();
if (!walletRemote) app.MapWalletGrpcTransport();
if (!identityRemote) app.MapIdentityGrpcTransport();
app.MapServiceDefaults();

await app.RunAsync();
