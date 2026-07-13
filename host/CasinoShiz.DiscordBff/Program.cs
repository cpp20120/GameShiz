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

builder.Services.AddDiceGrpcClient(backendUri);
builder.Services.AddNativeDiceGrpcClients(backendUri);
builder.Services.AddTransferGrpcClient(backendUri);
builder.Services.AddLeaderboardGrpcClient(backendUri);
builder.Services.AddBlackjackGrpcClient(backendUri);
builder.Services.AddHorseGrpcClient(backendUri);
builder.Services.AddPickGrpcClient(backendUri);
builder.Services.AddRedeemGrpcClient(backendUri);
builder.Services.AddChallengeGrpcClient(backendUri);
builder.Services.AddPokerGrpcClient(backendUri);
builder.Services.AddSecretHitlerGrpcClient(backendUri);
builder.Services.AddMetaGrpcClients(backendUri);

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
    .AddMetaDiscord();

var app = builder.Build();
app.UseDiscordBackend();
await app.RunAsync();
