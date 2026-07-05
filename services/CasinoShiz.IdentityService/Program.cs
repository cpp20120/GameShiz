using BotFramework.Host.Composition.Builder;
using CasinoShiz.Identity;
using CasinoShiz.Identity.Transport.Grpc;

var builder = WebApplication.CreateBuilder(args);
builder.AddBackendFramework().AddModule<IdentityModule>();
builder.Services.AddGrpc();

var app = builder.Build();
app.MapIdentityGrpcTransport();
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy", service = "casinoshiz-identity" }));
await app.RunAsync();
