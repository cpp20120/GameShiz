using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;

namespace BotFramework.Telegram.Abstractions.Tenancy;

public sealed record TelegramContainer(
    string ChatId,
    string UserId,
    string? TopicId = null,
    bool IsPrivateChat = false);

public interface ITelegramTenantContextResolver
{
    TenantContext Resolve(
        TelegramContainer container,
        RequestId requestId,
        RequestId correlationId);
}

/// <summary>
/// Default Telegram mapping: a chat is a tenant, a forum topic is a scope,
/// and non-topic chats use the main scope. Private chats receive a private
/// tenant so two users can never share a wallet boundary accidentally.
/// </summary>
public sealed class TelegramTenantContextResolver : ITelegramTenantContextResolver
{
    private readonly string? tenantKey;

    public TelegramTenantContextResolver(string? tenantKey = null)
    {
        this.tenantKey = string.IsNullOrWhiteSpace(tenantKey) ? null : tenantKey.Trim();
    }

    public TenantContext Resolve(TelegramContainer container, RequestId requestId, RequestId correlationId)
    {
        ArgumentNullException.ThrowIfNull(container);
        var tenantPrefix = tenantKey is null ? "telegram" : $"telegram:{tenantKey}";
        var tenant = container.IsPrivateChat
            ? TenantId.Create($"{tenantPrefix}:dm:{container.ChatId}")
            : TenantId.Create($"{tenantPrefix}:chat:{container.ChatId}");
        var scope = string.IsNullOrWhiteSpace(container.TopicId)
            ? ScopeId.Create("main")
            : ScopeId.Create($"topic:{container.TopicId}");
        return TenantContext.Create(
            tenant,
            scope,
            PlayerId.Create(container.UserId),
            BotChannel.Telegram,
            requestId,
            correlationId) with
        {
            ChannelContainerId = container.ChatId,
            ChannelTopicId = container.TopicId,
        };
    }
}
