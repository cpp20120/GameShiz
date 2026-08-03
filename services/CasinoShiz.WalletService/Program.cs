using BotFramework.Host.Composition.Builder;
using BotFramework.Host.Composition.ServiceDatabases;
using CasinoShiz.Wallet.Transport.Grpc;
using CasinoShiz.ServiceDefaults;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080, listen => listen.Protocols = HttpProtocols.Http1);
    options.ListenAnyIP(8081, listen => listen.Protocols = HttpProtocols.Http2);
});
builder.AddServiceDefaults();
builder.AddWalletServiceDatabase();
builder.AddFrameworkIntegrationMessaging("wallet");
builder.Services.AddGrpc();

var app = builder.Build();
app.UseTransportChannelContext();
app.MapWalletGrpcTransport();
app.MapServiceDefaults();
await app.RunAsync();
