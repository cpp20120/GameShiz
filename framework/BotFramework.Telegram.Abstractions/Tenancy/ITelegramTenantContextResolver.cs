using BotFramework.Contracts.Tenancy;

namespace BotFramework.Telegram.Abstractions.Tenancy;

public interface ITelegramTenantContextResolver
{
    TenantContext Resolve(
        TelegramContainer container,
        RequestId requestId,
        RequestId correlationId);
}