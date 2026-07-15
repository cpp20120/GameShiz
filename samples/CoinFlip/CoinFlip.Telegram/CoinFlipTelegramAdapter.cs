using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;
using BotFramework.Telegram.Abstractions.Tenancy;
using CoinFlip.Application;
using CoinFlip.Contracts;

namespace CoinFlip.Telegram;

public sealed class CoinFlipTelegramAdapter(
    ITelegramTenantContextResolver resolver,
    CoinFlipService service)
{
    public Task<CoinFlipReply> ExecuteAsync(TelegramContainer container, CancellationToken ct)
    {
        var requestId = RequestId.New();
        var context = resolver.Resolve(container, requestId, requestId);
        return service.ExecuteAsync(
            new CoinFlipCommand(context.TenantId, context.ScopeId, context.PlayerId!.Value, RequestId.New().ToString()),
            Random.Shared.Next(),
            ct);
    }
}
