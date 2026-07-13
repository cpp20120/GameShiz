using System.Text.Json;
using BotFramework.Host.Execution;
using Games.Pick.Application.Execution;

namespace Games.Pick.Infrastructure.Persistence;

public sealed class PickChainOfferEffectHandler : GameEffectHandler<PickChainOfferEffect>
{
    protected override async Task ApplyBatchAsync(
        IReadOnlyList<PickChainOfferEffect> effects,
        IGameExecutionContext context,
        CancellationToken ct)
    {
        foreach (var effect in effects)
        {
            var chain = effect.Chain;
            await context.ExecuteAsync(
                """
                INSERT INTO pick_chains
                    (id, user_id, chat_id, display_name, stake_for_next, depth, variants_json, backed_indices_json, expires_at)
                VALUES
                    (@Id, @UserId, @ChatId, @DisplayName, @StakeForNext, @Depth,
                     CAST(@variantsJson AS jsonb), CAST(@backedJson AS jsonb), @ExpiresAt)
                ON CONFLICT (id) DO NOTHING
                """,
                new
                {
                    chain.Id,
                    chain.UserId,
                    chain.ChatId,
                    chain.DisplayName,
                    chain.StakeForNext,
                    chain.Depth,
                    variantsJson = JsonSerializer.Serialize(chain.Variants),
                    backedJson = JsonSerializer.Serialize(chain.BackedIndices),
                    chain.ExpiresAt,
                }, ct).ConfigureAwait(false);
        }
    }
}
