using BotFramework.Rest;
using CasinoShiz.ServiceDefaults;
using Games.Admin.Rest;
using Games.Admin.Transport.Grpc;
using Games.Blackjack.Rest;
using Games.Blackjack.Transport.Grpc;
using Games.Challenges.Rest;
using Games.Challenges.Transport.Grpc;
using Games.Dice.Rest;
using Games.Dice.Transport.Grpc;
using Games.Horse.Rest;
using Games.Horse.Transport.Grpc;
using Games.Leaderboard.Rest;
using Games.Leaderboard.Transport.Grpc;
using Games.Meta.Rest;
using Games.Meta.Transport.Grpc;
using Games.NativeDice.Rest;
using Games.NativeDice.Transport.Grpc;
using Games.Pick.Rest;
using Games.Pick.Transport.Grpc;
using Games.Poker.Rest;
using Games.Poker.Transport.Grpc;
using Games.PixelBattle.Rest;
using Games.PixelBattle.Transport.Grpc;
using Games.Redeem.Rest;
using Games.Redeem.Transport.Grpc;
using Games.SecretHitler.Rest;
using Games.SecretHitler.Transport.Grpc;
using Games.Transfer.Rest;
using Games.Transfer.Transport.Grpc;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    var httpPort = builder.Configuration.GetValue("Rest:HttpPort", 8080);
    options.ListenAnyIP(httpPort, listen => listen.Protocols = HttpProtocols.Http1AndHttp2);

    var http2Port = builder.Configuration.GetValue("Rest:Http2Port", 8081);
    if (http2Port != httpPort)
        options.ListenAnyIP(http2Port, listen => listen.Protocols = HttpProtocols.Http2);
});

builder.AddServiceDefaults();
builder.AddRestFramework();

var backendAddress = builder.Configuration["Backend:GrpcAddress"]
    ?? throw new InvalidOperationException("Backend:GrpcAddress is required for the REST BFF.");
var backendUri = new Uri(backendAddress, UriKind.Absolute);

builder.Services.AddDiceGrpcClient(builder.Configuration.ResolveGameAddress("Dice", backendUri));
builder.Services.AddNativeDiceGrpcClients(
    builder.Configuration.ResolveGameAddress("DiceCube", backendUri),
    builder.Configuration.ResolveGameAddress("Darts", backendUri),
    builder.Configuration.ResolveGameAddress("Football", backendUri),
    builder.Configuration.ResolveGameAddress("Basketball", backendUri),
    builder.Configuration.ResolveGameAddress("Bowling", backendUri));
builder.Services.AddBlackjackGrpcClient(builder.Configuration.ResolveGameAddress("Blackjack", backendUri));
builder.Services.AddHorseGrpcClient(builder.Configuration.ResolveGameAddress("Horse", backendUri));
builder.Services.AddTransferGrpcClient(builder.Configuration.ResolveGameAddress("Transfer", backendUri));
builder.Services.AddChallengeGrpcClient(builder.Configuration.ResolveGameAddress("Challenges", backendUri));
builder.Services.AddPickGrpcClient(builder.Configuration.ResolveGameAddress("Pick", backendUri));
builder.Services.AddRedeemGrpcClient(builder.Configuration.ResolveGameAddress("Redeem", backendUri));
builder.Services.AddPixelBattleGrpcClient(builder.Configuration.ResolveGameAddress("PixelBattle", backendUri));
builder.Services.AddSecretHitlerGrpcClient(builder.Configuration.ResolveGameAddress("SecretHitler", backendUri));
builder.Services.AddMetaGrpcClients(builder.Configuration.ResolveGameAddress("Meta", backendUri));
builder.Services.AddAdminGrpcClients(builder.Configuration.ResolveGameAddress("Admin", backendUri));
builder.Services.AddPokerGrpcClient(builder.Configuration.ResolveGameAddress("Poker", backendUri));
builder.Services.AddLeaderboardGrpcClient(builder.Configuration.ResolveGameAddress("Leaderboard", backendUri));
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

var app = builder.Build();
app.UseRestFramework();
app.MapRestFramework();

await app.RunAsync();
