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

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080, listen => listen.Protocols = HttpProtocols.Http1);
    options.ListenAnyIP(8081, listen => listen.Protocols = HttpProtocols.Http2);
});
builder.AddServiceDefaults();
builder.Services.AddSingleton<HorseGifCache>();
builder.Services.AddScoped<IMiniGameSessionGhostHeal, MiniGameSessionGhostHeal>();

var framework = builder.AddBackendFramework()
    .AddModule<DiceModule>()
    .AddModule<DiceCubeModule>()
    .AddModule<DartsRemoteModule>()
    .AddModule<FootballModule>()
    .AddModule<BasketballModule>()
    .AddModule<BowlingModule>()
    .AddModule<TransferModule>()
    .AddModule<RedeemModule>()
    .AddModule<LeaderboardModule>()
    .AddModule<PixelBattleModule>()
    .AddModule<PickModule>()
    .AddModule<BlackjackModule>()
    .AddModule<HorseModule>()
    .AddModule<ChallengeModule>()
    .AddModule<PokerModule>()
    .AddModule<SecretHitlerModule>()
    .AddModule<MetaModule>()
    .AddModule<AdminModule>();

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
app.MapDiceGrpcTransport();
app.MapNativeDiceGrpcTransport();
app.MapTransferGrpcTransport();
app.MapRedeemGrpcTransport();
app.MapLeaderboardGrpcTransport();
app.MapPixelBattle();
app.MapPixelBattleGrpcTransport();
app.MapPickGrpcTransport();
app.MapBlackjackGrpcTransport();
app.MapHorseGrpcTransport();
app.MapChallengeGrpcTransport();
app.MapPokerGrpcTransport();
app.MapSecretHitlerGrpcTransport();
app.MapMetaGrpcTransport();
app.MapAdminGrpcTransport();
app.MapRenderHistory();
app.MapOperationsGrpcTransport();
app.MapRazorPages();
if (!walletRemote) app.MapWalletGrpcTransport();
if (!identityRemote) app.MapIdentityGrpcTransport();
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy", service = "casinoshiz-backend" }));

await app.RunAsync();
