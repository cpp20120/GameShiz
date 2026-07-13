using BotFramework.Host.Composition.ServiceDatabases;
using CasinoShiz.Identity;
using CasinoShiz.Identity.Transport.Grpc;
using CasinoShiz.ServiceDefaults;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080, listen => listen.Protocols = HttpProtocols.Http1);
    options.ListenAnyIP(8081, listen => listen.Protocols = HttpProtocols.Http2);
});
builder.AddServiceDefaults();
builder.AddOwnedPostgresDatabase(new IdentityMigrations());
builder.Services.AddSingleton<BotFramework.Contracts.Identity.IPlayerDirectory, PlayerDirectory>();
builder.Services.AddGrpc();

var app = builder.Build();
app.MapIdentityGrpcTransport();
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy", service = "casinoshiz-identity" }));
await app.RunAsync();
