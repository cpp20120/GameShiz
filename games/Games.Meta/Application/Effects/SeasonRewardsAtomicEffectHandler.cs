using System.Globalization;
using System.Text.Json;
using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Execution;
using Dapper;
using Games.Meta.Application.Clans;
using Games.Meta.Application.Models;
using Games.Meta.Domain.Seasons;

namespace Games.Meta.Application.Effects;

internal abstract class SeasonRewardsAtomicEffectHandler<TEffect> : AtomicEffectHandler<TEffect>
    where TEffect : class, IAtomicEffect
{
    protected static Task<string?> ConfigAsync(IAtomicEffectContext context, long seasonId, CancellationToken ct) =>
        context.QuerySingleOrDefaultAsync<string?>("SELECT config::text FROM meta_seasons WHERE id = @seasonId FOR UPDATE", new { seasonId }, ct);

    protected static async Task CreditAsync(
        IAtomicEffectContext context,
        long userId,
        long chatId,
        string displayName,
        int amount,
        string reason,
        string operationId,
        CancellationToken ct)
    {
        if (amount <= 0) return;
        var wallet = context.Wallet ?? throw new InvalidOperationException("Wallet boundary is not configured.");
        await wallet.EnsureUserAsync(userId, chatId, displayName, ct);
        var result = await wallet.ApplyBatchAsync(
            userId,
            chatId,
            [new WalletBatchEffect(WalletBatchEffectKind.Credit, amount, reason)],
            operationId,
            ct);
        if (!result.Applied)
            throw new InvalidOperationException("Season reward wallet rejected the credit.");
    }

    protected static Task AppendHistoryAsync(IAtomicEffectContext context, long seasonId, string eventType, object payload, CancellationToken ct) =>
        context.ExecuteAsync(
            "INSERT INTO meta_event_log (event_type, aggregate_type, aggregate_id, season_id, payload) VALUES (@eventType, 'season', @aggregateId, @seasonId, CAST(@payload AS jsonb))",
            new { eventType, aggregateId = seasonId.ToString(CultureInfo.InvariantCulture), seasonId, payload = JsonSerializer.Serialize(payload) }, ct);

}
