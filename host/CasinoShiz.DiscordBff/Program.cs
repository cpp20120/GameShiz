using BotFramework.Discord.Composition;
using CasinoShiz.ServiceDefaults;
using Games.Basketball.Discord;
using Games.Blackjack.Discord;
using Games.Blackjack.Transport.Grpc;
using Games.Leaderboard.Discord;
using Games.Leaderboard.Transport.Grpc;
using Games.Redeem.Discord;
using Games.Redeem.Transport.Grpc;
using Games.Transfer.Discord;
using Games.Transfer.Transport.Grpc;
using Games.Bowling.Discord;
using Games.Darts.Discord;
using Games.Dice.Discord;
using Games.Dice.Transport.Grpc;
using Games.DiceCube.Discord;
using Games.Football.Discord;
using Games.NativeDice.Transport.Grpc;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddDiscordBackend();

var backendAddress = builder.Configuration["Backend:GrpcAddress"]
    ?? throw new InvalidOperationException("Set Backend:GrpcAddress for the Discord BFF.");
var backendUri = new Uri(backendAddress, UriKind.Absolute);

builder.Services.AddDiceGrpcClient(backendUri);
builder.Services.AddNativeDiceGrpcClients(backendUri);
builder.Services.AddTransferGrpcClient(backendUri);
builder.Services.AddRedeemGrpcClient(backendUri);
builder.Services.AddLeaderboardGrpcClient(backendUri);
builder.Services.AddBlackjackGrpcClient(backendUri);
builder.Services
    .AddDiceDiscord()
    .AddDiceCubeDiscord()
    .AddDartsDiscord()
    .AddFootballDiscord()
    .AddBasketballDiscord()
    .AddBowlingDiscord()
    .AddTransferDiscord()
    .AddRedeemDiscord()
    .AddLeaderboardDiscord()
    .AddBlackjackDiscord();

var app = builder.Build();
app.UseDiscordBackend();
await app.RunAsync();
