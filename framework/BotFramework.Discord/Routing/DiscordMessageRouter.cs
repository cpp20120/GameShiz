using Microsoft.Extensions.Logging;
using BotFramework.Discord.Commands;
using BotFramework.Discord.Hosting;
using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.RateLimiting;
using BotFramework.Contracts.Tenancy;
using BotFramework.Discord.Abstractions;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;

namespace BotFramework.Discord.Routing;

public sealed partial class DiscordMessageRouter(
    IEnumerable<IDiscordMessageHandler> handlers,
    ILogger<DiscordMessageRouter> logger,
    IRateLimiter rateLimiter,
    IDiscordTenantContextResolver tenantResolver,
    ITenantContextAccessor tenantContext,
    RateLimitRequestState requestState)
{
    public async Task RouteAsync(DiscordMessageContext context)
    {
        var requestId = RequestId.Create(string.Create(
            CultureInfo.InvariantCulture,
            $"discord:message:{context.Message.Id}"));
        var resolvedTenant = DiscordTenantContext.Resolve(context.Message, tenantResolver, requestId);
        using var tenantScope = tenantContext.Push(resolvedTenant);
        var provisioner = context.Services.GetService<ITenantContextProvisioner>();
        if (provisioner is not null)
            await provisioner.EnsureAsync(resolvedTenant, context.CancellationToken).ConfigureAwait(false);
        using var metadataScope = RequestMetadataContext.Push(
            RequestMetadata.FromTenantContext(resolvedTenant, "discord"));
        var decision = await rateLimiter.CheckAsync(
            new RateLimitRequest(
                resolvedTenant.TenantId,
                resolvedTenant.PlayerId,
                BotChannel.Discord,
                "message-command"),
            context.CancellationToken).ConfigureAwait(false);
        if (!decision.Allowed)
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(decision.RetryAfter.TotalSeconds));
            await DiscordCommand.ReplyAsync(
                context,
                DiscordLocalization.Format("rate.limited", context.CultureCode, seconds),
                isError: true);
            return;
        }
        requestState.LeaseGranted = true;

        foreach (var handler in handlers)
        {
            if (!handler.CanHandle(context)) continue;
            await handler.HandleAsync(context);
            return;
        }

        LogUnhandledMessage(logger, context.Message.Id, context.Message.Author.Id);
    }

    [LoggerMessage(LogLevel.Debug, "No Discord handler accepted message {MessageId} from user {UserId}")]
    private static partial void LogUnhandledMessage(ILogger logger, ulong messageId, ulong userId);
}
