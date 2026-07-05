using BotFramework.Host.Composition.Builder;
using Games.Dice.Telegram;
using Games.Dice.Transport.Grpc;
using Games.NativeDice.Transport.Grpc;
using Games.DiceCube.Telegram;
using Games.Darts.Telegram;
using Games.Football.Telegram;
using Games.Basketball.Telegram;
using Games.Bowling.Telegram;
using Games.Transfer.Telegram;
using Games.Transfer.Transport.Grpc;
using Games.Redeem.Telegram;
using Games.Redeem.Transport.Grpc;
using Games.Leaderboard.Telegram;
using Games.Leaderboard.Transport.Grpc;
using Games.PixelBattle.Telegram;
using Games.Pick.Telegram;
using Games.Pick.Transport.Grpc;
using Games.Blackjack.Telegram;
using Games.Blackjack.Transport.Grpc;
using Games.Horse.Telegram;
using Games.Horse.Transport.Grpc;
using Games.Challenges.Telegram;
using Games.Challenges.Transport.Grpc;
using Games.Poker.Telegram;
using Games.Poker.Transport.Grpc;
using Games.SecretHitler.Telegram;
using Games.SecretHitler.Transport.Grpc;
using Games.Meta.Telegram;
using Games.Meta.Transport.Grpc;
using Games.Admin.Telegram;
using Games.Admin.Transport.Grpc;
using CasinoShiz.Identity.Transport.Grpc;
using CasinoShiz.Wallet.Transport.Grpc;

var builder = WebApplication.CreateBuilder(args);

var backendAddress = builder.Configuration["Backend:GrpcAddress"]
    ?? throw new InvalidOperationException("Set Backend:GrpcAddress for the Telegram BFF.");
builder.Services.AddDiceGrpcClient(new Uri(backendAddress, UriKind.Absolute));
builder.Services.AddNativeDiceGrpcClients(new Uri(backendAddress, UriKind.Absolute));
builder.Services.AddTransferGrpcClient(new Uri(backendAddress, UriKind.Absolute));
builder.Services.AddRedeemGrpcClient(new Uri(backendAddress, UriKind.Absolute));
builder.Services.AddLeaderboardGrpcClient(new Uri(backendAddress, UriKind.Absolute));
builder.Services.AddPickGrpcClient(new Uri(backendAddress, UriKind.Absolute));
builder.Services.AddBlackjackGrpcClient(new Uri(backendAddress, UriKind.Absolute));
builder.Services.AddHorseGrpcClient(new Uri(backendAddress, UriKind.Absolute));
builder.Services.AddChallengeGrpcClient(new Uri(backendAddress, UriKind.Absolute));
builder.Services.AddPokerGrpcClient(new Uri(backendAddress, UriKind.Absolute));
builder.Services.AddSecretHitlerGrpcClient(new Uri(backendAddress, UriKind.Absolute));
builder.Services.AddMetaGrpcClients(new Uri(backendAddress, UriKind.Absolute));
builder.Services.AddAdminGrpcClients(new Uri(backendAddress, UriKind.Absolute));
var identityAddress = builder.Configuration["Services:Identity:Address"] ?? backendAddress;
var walletAddress = builder.Configuration["Services:Wallet:Address"] ?? backendAddress;
builder.Services.AddIdentityGrpcClient(new Uri(identityAddress, UriKind.Absolute));
builder.Services.AddWalletGrpcClients(new Uri(walletAddress, UriKind.Absolute));

builder.AddTelegramBff()
    .AddModule<DiceTelegramModule>()
    .AddModule<DiceCubeTelegramModule>()
    .AddModule<DartsTelegramModule>()
    .AddModule<FootballTelegramModule>()
    .AddModule<BasketballTelegramModule>()
    .AddModule<BowlingTelegramModule>()
    .AddModule<TransferTelegramModule>()
    .AddModule<RedeemTelegramModule>()
    .AddModule<LeaderboardTelegramModule>()
    .AddModule<PixelBattleTelegramModule>()
    .AddModule<PickTelegramModule>()
    .AddModule<BlackjackTelegramModule>()
    .AddModule<HorseTelegramModule>()
    .AddModule<ChallengeTelegramModule>()
    .AddModule<PokerTelegramModule>()
    .AddModule<SecretHitlerTelegramModule>()
    .AddModule<MetaTelegramModule>()
    .AddModule<AdminTelegramModule>();

var app = builder.Build();
app.UseTelegramBff();
await app.RunAsync();
