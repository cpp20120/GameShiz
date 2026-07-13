using BotFramework.Discord.Composition;
using CasinoShiz.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddDiscordBackend();

var app = builder.Build();
app.UseDiscordBackend();
await app.RunAsync();
