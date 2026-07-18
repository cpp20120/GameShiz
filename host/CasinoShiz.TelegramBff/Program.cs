using BotFramework.Host.Composition.Builder;
using BotFramework.Host.Configuration.RuntimeTuning;
using BotFramework.Host.Persistence.Connections;
using BotFramework.Scheduling.Quartz;
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
using CasinoShiz.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddSingleton<INpgsqlConnectionFactory, NpgsqlConnectionFactory>();
builder.Services.AddSingleton<RuntimeTuningAccessor>();
builder.Services.AddSingleton<IRuntimeTuningAccessor>(sp => sp.GetRequiredService<RuntimeTuningAccessor>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<RuntimeTuningAccessor>());

var schedulerPostgres = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required for Telegram Quartz scheduling.");
builder.Services.AddQuartzGameScheduling(schedulerPostgres, "CasinoShizTelegram");
builder.Services.AddQuartzRecurringCommandBootstrapper();

var backendAddress = builder.Configuration["Backend:GrpcAddress"]
    ?? throw new InvalidOperationException("Set Backend:GrpcAddress for the Telegram BFF.");
var backendUri = new Uri(backendAddress, UriKind.Absolute);
builder.Services.AddDiceGrpcClient(builder.Configuration.ResolveGameAddress("Dice", backendUri));
builder.Services.AddNativeDiceGrpcClients(
    builder.Configuration.ResolveGameAddress("DiceCube", backendUri),
    builder.Configuration.ResolveGameAddress("Darts", backendUri),
    builder.Configuration.ResolveGameAddress("Football", backendUri),
    builder.Configuration.ResolveGameAddress("Basketball", backendUri),
    builder.Configuration.ResolveGameAddress("Bowling", backendUri));
builder.Services.AddTransferGrpcClient(builder.Configuration.ResolveGameAddress("Transfer", backendUri));
builder.Services.AddRedeemGrpcClient(builder.Configuration.ResolveGameAddress("Redeem", backendUri));
builder.Services.AddLeaderboardGrpcClient(builder.Configuration.ResolveGameAddress("Leaderboard", backendUri));
builder.Services.AddPickGrpcClient(builder.Configuration.ResolveGameAddress("Pick", backendUri));
builder.Services.AddBlackjackGrpcClient(builder.Configuration.ResolveGameAddress("Blackjack", backendUri));
builder.Services.AddHorseGrpcClient(builder.Configuration.ResolveGameAddress("Horse", backendUri));
builder.Services.AddChallengeGrpcClient(builder.Configuration.ResolveGameAddress("Challenges", backendUri));
builder.Services.AddPokerGrpcClient(builder.Configuration.ResolveGameAddress("Poker", backendUri));
builder.Services.AddSecretHitlerGrpcClient(builder.Configuration.ResolveGameAddress("SecretHitler", backendUri));
builder.Services.AddMetaGrpcClients(builder.Configuration.ResolveGameAddress("Meta", backendUri));
builder.Services.AddAdminGrpcClients(builder.Configuration.ResolveGameAddress("Admin", backendUri));
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
