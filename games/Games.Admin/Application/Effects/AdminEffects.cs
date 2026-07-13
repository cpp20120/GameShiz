using BotFramework.Host.Admin.Execution;
using BotFramework.Sdk.Admin.Execution;
using Dapper;
using Games.Admin.Infrastructure.Models;

namespace Games.Admin.Application.Effects;

public sealed record ClearChatBetsAdminEffect(long ChatId) : IAdminEffect;

internal sealed class ClearChatBetsAdminEffectHandler : AdminEffectHandler<ClearChatBetsAdminEffect>
{
    protected override async Task ApplyAsync(
        ClearChatBetsAdminEffect effect,
        IAdminExecutionContext context,
        CancellationToken ct)
    {
        var deleted = new List<PendingChatBet>();
        deleted.AddRange(await context.QueryAsync<PendingChatBet>(
            """
            DELETE FROM dicecube_bets
            WHERE chat_id = @chatId
            RETURNING 'dicecube' AS GameId, user_id AS UserId, chat_id AS ChatId, amount AS Amount, NULL::integer AS BotMessageId
            """,
            new { effect.ChatId }, ct).ConfigureAwait(false));
        deleted.AddRange(await context.QueryAsync<PendingChatBet>(
            """
            DELETE FROM football_bets
            WHERE chat_id = @chatId
            RETURNING 'football' AS GameId, user_id AS UserId, chat_id AS ChatId, amount AS Amount, NULL::integer AS BotMessageId
            """,
            new { effect.ChatId }, ct).ConfigureAwait(false));
        deleted.AddRange(await context.QueryAsync<PendingChatBet>(
            """
            DELETE FROM basketball_bets
            WHERE chat_id = @chatId
            RETURNING 'basketball' AS GameId, user_id AS UserId, chat_id AS ChatId, amount AS Amount, NULL::integer AS BotMessageId
            """,
            new { effect.ChatId }, ct).ConfigureAwait(false));
        deleted.AddRange(await context.QueryAsync<PendingChatBet>(
            """
            DELETE FROM bowling_bets
            WHERE chat_id = @chatId
            RETURNING 'bowling' AS GameId, user_id AS UserId, chat_id AS ChatId, amount AS Amount, NULL::integer AS BotMessageId
            """,
            new { effect.ChatId }, ct).ConfigureAwait(false));
        deleted.AddRange(await context.QueryAsync<PendingChatBet>(
            """
            DELETE FROM darts_rounds
            WHERE chat_id = @chatId AND status IN (@queued, @awaiting)
            RETURNING 'darts' AS GameId, user_id AS UserId, chat_id AS ChatId, amount AS Amount, bot_message_id AS BotMessageId
            """,
            new
            {
                effect.ChatId,
                queued = (short)Games.Darts.Domain.Results.DartsRoundStatus.Queued,
                awaiting = (short)Games.Darts.Domain.Results.DartsRoundStatus.AwaitingOutcome,
            }, ct).ConfigureAwait(false));

        foreach (var bet in deleted)
        {
            var balance = await context.QuerySingleOrDefaultAsync<int?>(
                """
                UPDATE users
                SET coins = coins + @amount, version = version + 1, updated_at = now()
                WHERE telegram_user_id = @userId AND balance_scope_id = @chatId
                RETURNING coins
                """,
                new { userId = bet.UserId, chatId = bet.ChatId, amount = bet.Amount }, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Wallet {bet.UserId}:{bet.ChatId} is missing while refunding a pending bet.");

            await context.ExecuteAsync(
                """
                INSERT INTO economics_ledger
                    (telegram_user_id, balance_scope_id, delta, balance_after, reason)
                VALUES (@userId, @chatId, @amount, @balance, @reason)
                """,
                new
                {
                    userId = bet.UserId,
                    chatId = bet.ChatId,
                    amount = bet.Amount,
                    balance,
                    reason = $"admin.clearbets.{bet.GameId}",
                }, ct).ConfigureAwait(false);
        }

        context.SetOutput("bets", deleted);
    }
}

public sealed record DisplayNameOverrideAdminEffect(string OriginalName, string? NewName) : IAdminEffect;

internal sealed class DisplayNameOverrideAdminEffectHandler : AdminEffectHandler<DisplayNameOverrideAdminEffect>
{
    protected override Task ApplyAsync(
        DisplayNameOverrideAdminEffect effect,
        IAdminExecutionContext context,
        CancellationToken ct) =>
        effect.NewName is null
            ? context.ExecuteAsync(
                "DELETE FROM display_name_overrides WHERE original_name = @originalName",
                new { effect.OriginalName }, ct)
            : context.ExecuteAsync(
                """
                INSERT INTO display_name_overrides (original_name, new_name)
                VALUES (@originalName, @newName)
                ON CONFLICT (original_name) DO UPDATE SET new_name = EXCLUDED.new_name
                """,
                new { effect.OriginalName, effect.NewName }, ct);
}
