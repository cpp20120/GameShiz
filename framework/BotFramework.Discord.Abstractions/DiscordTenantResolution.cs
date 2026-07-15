using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;

namespace BotFramework.Discord.Abstractions;

public sealed record DiscordContainer(
    string GuildId,
    string ChannelId,
    string? ThreadId,
    string UserId,
    bool IsDirectMessage = false);

public interface IDiscordTenantContextResolver
{
    TenantContext Resolve(
        DiscordContainer container,
        RequestId requestId,
        RequestId correlationId);
}

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
        var scopeValue = container.IsDirectMessage
            ? "main"
            : $"channel:{container.ChannelId}{(string.IsNullOrWhiteSpace(container.ThreadId) ? string.Empty : $":thread:{container.ThreadId}")}";
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
}
