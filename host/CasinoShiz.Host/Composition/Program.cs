// ─────────────────────────────────────────────────────────────────────────────
// CasinoShiz.Host — Program composition root.
//
// This is the entire Host for the pilot distribution: standard ASP.NET Core
// WebApplication bootstrap, one call to AddBotFramework() to register every
// framework-owned service, one AddModule<T>() per game this distribution
// ships, then Build() + UseBotFramework() + Run().
//
// Bringing up another distribution (a party-games bot, a trading bot) is the
// same three lines plus a different module list. No per-module DI wiring
// leaks here — modules own their own ConfigureServices.
// ─────────────────────────────────────────────────────────────────────────────

using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Builder;
using Games.Dice.Telegram;
using Games.DiceCube.Telegram;
using Games.Darts.Telegram;
using Games.Darts.Telegram.Delivery;
using Games.Football.Telegram;
using Games.Basketball.Telegram;
using Games.Bowling.Telegram;
using Games.Transfer.Telegram;
using Games.Redeem.Telegram;
using Games.Leaderboard.Telegram;
using Games.PixelBattle.Telegram;
using Games.Pick.Telegram;
using Games.Blackjack.Telegram;
using Games.Horse.Telegram;
using Games.Challenges.Telegram;
using Games.Meta.Telegram;
using Games.Meta.Application.Tournaments;
using Games.Admin.Telegram;
using CasinoShiz.Identity;
using Games.Poker.Telegram;
using Games.SecretHitler.Telegram;
using BotFramework.Telegram.Composition;
using BotFramework.Rendering;
using BotFramework.Rest;
using Games.Dice.Rest;
using Games.NativeDice.Rest;
using Games.Blackjack.Rest;
using Games.Horse.Rest;
using Games.Transfer.Rest;
using Games.Challenges.Rest;
using Games.Pick.Rest;
using Games.Redeem.Rest;
using Games.PixelBattle.Rest;
using Games.SecretHitler.Rest;
using Games.Meta.Rest;
using Games.Admin.Rest;
using Games.Poker.Rest;
using Games.Leaderboard.Rest;

var builder = WebApplication.CreateBuilder(args);

builder.AddDurableWorkflows(typeof(TournamentWorkflowHandler).Assembly);

builder.Services.AddRazorPages();
builder.AddRestFramework();
builder.Services.AddSingleton<HorseGifCache>();
builder.Services.AddScoped<IMiniGameSessionGhostHeal, MiniGameSessionGhostHeal>();

builder.AddBotFramework()
    .AddModule<DebugModule>()
    .AddModule<DiceModule>()
    .AddModule<DiceTelegramModule>()
    .AddModule<DiceCubeModule>()
    .AddModule<DiceCubeTelegramModule>()
    .AddModule<DartsModule>()
    .AddModule<DartsTelegramModule>()
    .AddModule<DartsDeliveryTelegramModule>()
    .AddModule<FootballModule>()
    .AddModule<FootballTelegramModule>()
    .AddModule<BasketballModule>()
    .AddModule<BasketballTelegramModule>()
    .AddModule<BowlingModule>()
    .AddModule<BowlingTelegramModule>()
    .AddModule<ChallengeModule>()
    .AddModule<ChallengeTelegramModule>()
    .AddModule<BlackjackModule>()
    .AddModule<BlackjackTelegramModule>()
    .AddModule<HorseModule>()
    .AddModule<HorseTelegramModule>()
    .AddModule<PokerModule>()
    .AddModule<PokerTelegramModule>()
    .AddModule<SecretHitlerModule>()
    .AddModule<SecretHitlerTelegramModule>()
    .AddModule<RedeemModule>()
    .AddModule<RedeemTelegramModule>()
    .AddModule<LeaderboardModule>()
    .AddModule<LeaderboardTelegramModule>()
    .AddModule<TransferModule>()
    .AddModule<TransferTelegramModule>()
    .AddModule<PixelBattleModule>()
    .AddModule<PixelBattleTelegramModule>()
    .AddModule<PickModule>()
    .AddModule<PickTelegramModule>()
    .AddModule<MetaModule>()
    .AddModule<MetaTelegramModule>()
    .AddModule<AdminModule>()
    .AddModule<AdminTelegramModule>()
    .AddModule<IdentityModule>();

builder.Services.AddDiceRest();
builder.Services.AddNativeDiceRest();
builder.Services.AddBlackjackRest();
builder.Services.AddHorseRest();
builder.Services.AddTransferRest();
builder.Services.AddChallengesRest();
builder.Services.AddPickRest();
builder.Services.AddRedeemRest();
builder.Services.AddPixelBattleRest();
builder.Services.AddSecretHitlerRest();
builder.Services.AddMetaRest();
builder.Services.AddAdminRest();
builder.Services.AddPokerRest();
builder.Services.AddLeaderboardRest();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;


#pragma warning disable ASPDEPR005
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
#pragma warning restore ASPDEPR005
});


var app = builder.Build();

app.UseForwardedHeaders();
app.UseRestFramework();
app.UseBotFramework();
app.MapRestFramework();
app.MapRenderHistory();
app.UseStaticFiles();
app.MapRazorPages();
app.MapPixelBattle();

await app.RunAsync();
