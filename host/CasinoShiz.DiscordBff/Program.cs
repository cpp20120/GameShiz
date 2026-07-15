using Games.Admin.Discord;
using Games.Admin.Transport.Grpc;
using Games.PixelBattle.Discord;
using Games.PixelBattle.Transport.Grpc;
using Games.Challenges.Discord;
using Games.Challenges.Transport.Grpc;
using Games.Horse.Discord;
using Games.Horse.Transport.Grpc;
using Games.Meta.Discord;
using Games.Meta.Transport.Grpc;
using Games.Pick.Discord;
using Games.Pick.Transport.Grpc;
using Games.Poker.Discord;
using Games.Poker.Transport.Grpc;
using Games.Redeem.Discord;
using Games.Redeem.Transport.Grpc;
using Games.SecretHitler.Discord;
using Games.SecretHitler.Transport.Grpc;
using BotFramework.Discord.Composition;
using CasinoShiz.ServiceDefaults;
using Games.Blackjack.Discord;
using Games.Blackjack.Transport.Grpc;
using Games.Basketball.Discord;
using Games.Bowling.Discord;
using Games.Darts.Discord;
using Games.Dice.Discord;
using Games.Dice.Transport.Grpc;
using Games.DiceCube.Discord;
using Games.Football.Discord;
using Games.Leaderboard.Discord;
using Games.Leaderboard.Transport.Grpc;
using Games.NativeDice.Transport.Grpc;
using Games.Transfer.Discord;
using Games.Transfer.Transport.Grpc;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddDiscordBackend();

var backendAddress = builder.Configuration["Backend:GrpcAddress"]
    ?? throw new InvalidOperationException("Set Backend:GrpcAddress for the Discord BFF.");
var backendUri = new Uri(backendAddress, UriKind.Absolute);

builder.Services.AddDiceGrpcClient(builder.Configuration.ResolveGameAddress("Dice", backendUri));
builder.Services.AddNativeDiceGrpcClients(
    builder.Configuration.ResolveGameAddress("DiceCube", backendUri),
    builder.Configuration.ResolveGameAddress("Darts", backendUri),
    builder.Configuration.ResolveGameAddress("Football", backendUri),
    builder.Configuration.ResolveGameAddress("Basketball", backendUri),
    builder.Configuration.ResolveGameAddress("Bowling", backendUri));
builder.Services.AddTransferGrpcClient(builder.Configuration.ResolveGameAddress("Transfer", backendUri));
builder.Services.AddLeaderboardGrpcClient(builder.Configuration.ResolveGameAddress("Leaderboard", backendUri));
builder.Services.AddBlackjackGrpcClient(builder.Configuration.ResolveGameAddress("Blackjack", backendUri));
builder.Services.AddHorseGrpcClient(builder.Configuration.ResolveGameAddress("Horse", backendUri));
builder.Services.AddPickGrpcClient(builder.Configuration.ResolveGameAddress("Pick", backendUri));
builder.Services.AddRedeemGrpcClient(builder.Configuration.ResolveGameAddress("Redeem", backendUri));
builder.Services.AddChallengeGrpcClient(builder.Configuration.ResolveGameAddress("Challenges", backendUri));
builder.Services.AddPokerGrpcClient(builder.Configuration.ResolveGameAddress("Poker", backendUri));
builder.Services.AddSecretHitlerGrpcClient(builder.Configuration.ResolveGameAddress("SecretHitler", backendUri));
builder.Services.AddMetaGrpcClients(builder.Configuration.ResolveGameAddress("Meta", backendUri));
builder.Services.AddPixelBattleGrpcClient(builder.Configuration.ResolveGameAddress("PixelBattle", backendUri));
builder.Services.AddAdminGrpcClients(builder.Configuration.ResolveGameAddress("Admin", backendUri));

builder.Services
    .AddDiceDiscord()
    .AddDiceCubeDiscord()
    .AddDartsDiscord()
    .AddFootballDiscord()
    .AddBasketballDiscord()
    .AddBowlingDiscord()
    .AddTransferDiscord()
    .AddLeaderboardDiscord()
    .AddBlackjackDiscord()
    .AddHorseDiscord()
    .AddPickDiscord()
    .AddRedeemDiscord()
    .AddChallengesDiscord()
    .AddPokerDiscord()
    .AddSecretHitlerDiscord()
    .AddMetaDiscord()
    .AddPixelBattleDiscord()
    .AddAdminDiscord();

var app = builder.Build();
app.UseDiscordBackend();
await app.RunAsync();
