using BotFramework.Rest;
using CasinoShiz.ServiceDefaults;
using Games.Dice.Rest;
using Games.Dice.Transport.Grpc;
using Games.Leaderboard.Rest;
using Games.Leaderboard.Transport.Grpc;
using Games.Poker.Rest;
using Games.Poker.Transport.Grpc;
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
builder.Services.AddPokerGrpcClient(builder.Configuration.ResolveGameAddress("Poker", backendUri));
builder.Services.AddLeaderboardGrpcClient(builder.Configuration.ResolveGameAddress("Leaderboard", backendUri));
builder.Services.AddDiceRest();
builder.Services.AddPokerRest();
builder.Services.AddLeaderboardRest();

var app = builder.Build();
app.UseRestFramework();
app.MapRestFramework();

await app.RunAsync();
