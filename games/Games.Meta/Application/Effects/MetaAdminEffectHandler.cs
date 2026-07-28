using System.Globalization;
using System.Text.Json;
using BotFramework.Host.Admin.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Admin.Execution;
using Dapper;
using Games.Meta.Application.Clans;
using Games.Meta.Application.Models;
using Games.Meta.Infrastructure.Catalog;
using Games.Meta.Domain.Seasons;

namespace Games.Meta.Application.Effects;

internal abstract class MetaAdminEffectHandler<TEffect> : AdminEffectHandler<TEffect>
    where TEffect : class, IAdminEffect
{
    protected static Task AppendHistoryAsync(
        IAdminExecutionContext context,
        string eventType,
        string aggregateType,
        string aggregateId,
        long? seasonId,
        object payload,
        CancellationToken ct) =>
        context.ExecuteAsync(
            """
            INSERT INTO meta_event_log
                (event_type, aggregate_type, aggregate_id, season_id, user_id, payload)
            VALUES (@eventType, @aggregateType, @aggregateId, @seasonId, @userId, CAST(@payloadJson AS jsonb))
            """,
            new
            {
                eventType,
                aggregateType,
                aggregateId,
                seasonId,
                userId = context.Actor.Id,
                payloadJson = JsonSerializer.Serialize(payload),
            },
            ct);

    protected static async Task CreditAsync(
        IAdminExecutionContext context,
        long userId,
        long chatId,
        string displayName,
        int amount,
        string reason,
        string operationId,
        CancellationToken ct)
    {
        var wallet = context.Wallet ?? throw new InvalidOperationException("Wallet boundary is not configured.");
        await wallet.EnsureUserAsync(userId, chatId, displayName, ct);
        var result = await wallet.ApplyBatchAsync(
            userId,
            chatId,
            [new WalletBatchEffect(WalletBatchEffectKind.Credit, amount, reason)],
            operationId,
            ct);
        if (!result.Applied)
            throw new InvalidOperationException("Meta reward wallet rejected the credit.");
    }

}
