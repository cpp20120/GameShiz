using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;

namespace BotFramework.Discord.Abstractions;

/// <summary>
/// Default Discord mapping: guild is the tenant and channel/thread is the
/// scope. A DM gets a private tenant and a main scope for that container.
/// </summary>
public sealed class DiscordTenantContextResolver : IDiscordTenantContextResolver
{
    public TenantContext Resolve(DiscordContainer container, RequestId requestId, RequestId correlationId)
    {
        ArgumentNullException.ThrowIfNull(container);
        var tenant = container.IsDirectMessage
            ? TenantId.Create($"discord:dm:{container.UserId}")
            : TenantId.Create($"discord:guild:{container.GuildId}");
        var scopeValue = BuildScopeValue(container);
        return TenantContext.Create(
            tenant,
            ScopeId.Create(scopeValue),
            PlayerId.Create(container.UserId),
            BotChannel.Discord,
            requestId,
            correlationId) with
        {
            ChannelContainerId = container.ChannelId,
            ChannelTopicId = container.ThreadId,
        };
    }

    private static string BuildScopeValue(DiscordContainer container)
    {
        if (container.IsDirectMessage)
            return "main";

        var thread = string.IsNullOrWhiteSpace(container.ThreadId)
            ? string.Empty
            : $":thread:{container.ThreadId}";
        return $"channel:{container.ChannelId}{thread}";
    }
}
