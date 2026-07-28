using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;

namespace BotFramework.Discord.Abstractions;

public interface IDiscordTenantContextResolver
{
    TenantContext Resolve(
        DiscordContainer container,
        RequestId requestId,
        RequestId correlationId);
}
