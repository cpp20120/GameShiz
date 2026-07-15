using BotFramework.Contracts.Messaging;
using Microsoft.AspNetCore.Builder;

namespace CasinoShiz.ServiceDefaults;

public static class TransportChannelApplicationBuilderExtensions
{
    public static IApplicationBuilder UseTransportChannelContext(this IApplicationBuilder app) =>
        app.Use(async (httpContext, next) =>
        {
            var value = httpContext.Request.Headers["x-casino-channel"].FirstOrDefault();
            var channel = Enum.TryParse<BotChannel>(value, ignoreCase: true, out var parsed)
                && Enum.IsDefined(parsed)
                ? parsed
                : BotChannel.Telegram;
            using var scope = BotChannelContext.Push(channel);
            await next();
        });
}
