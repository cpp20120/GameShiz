using BotFramework.Host.Composition.Builder;
using CasinoShiz.Wallet.Transport.Grpc;

var builder = WebApplication.CreateBuilder(args);
builder.AddBackendFramework();
builder.Services.AddGrpc();

var app = builder.Build();
app.MapWalletGrpcTransport();
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy", service = "casinoshiz-wallet" }));
await app.RunAsync();
