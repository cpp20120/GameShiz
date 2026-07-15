using BotFramework.Contracts.Tenancy;
using BotFramework.Discord.Abstractions;
using CoinFlip.Application;
using CoinFlip.Contracts;

namespace CoinFlip.Discord;

public sealed class CoinFlipDiscordAdapter(
    IDiscordTenantContextResolver resolver,
    CoinFlipService service)
{
    public Task<CoinFlipReply> ExecuteAsync(DiscordContainer container, CancellationToken ct)
    {
        var requestId = RequestId.New();
        var context = resolver.Resolve(container, requestId, requestId);
        return service.ExecuteAsync(
            new CoinFlipCommand(context.TenantId, context.ScopeId, context.PlayerId!.Value, RequestId.New().ToString()),
            Random.Shared.Next(),
            ct);
    }
}
