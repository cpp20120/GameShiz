using BotFramework.Host.Admin.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Admin.Execution;
using Dapper;
using Games.Admin.Infrastructure.Models;

namespace Games.Admin.Application.Effects;

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
            new { effect.ChatId }, ct));
        deleted.AddRange(await context.QueryAsync<PendingChatBet>(
            """
            DELETE FROM football_bets
            WHERE chat_id = @chatId
            RETURNING 'football' AS GameId, user_id AS UserId, chat_id AS ChatId, amount AS Amount, NULL::integer AS BotMessageId
            """,
            new { effect.ChatId }, ct));
        deleted.AddRange(await context.QueryAsync<PendingChatBet>(
            """
            DELETE FROM basketball_bets
            WHERE chat_id = @chatId
            RETURNING 'basketball' AS GameId, user_id AS UserId, chat_id AS ChatId, amount AS Amount, NULL::integer AS BotMessageId
            """,
            new { effect.ChatId }, ct));
        deleted.AddRange(await context.QueryAsync<PendingChatBet>(
            """
            DELETE FROM bowling_bets
            WHERE chat_id = @chatId
            RETURNING 'bowling' AS GameId, user_id AS UserId, chat_id AS ChatId, amount AS Amount, NULL::integer AS BotMessageId
            """,
            new { effect.ChatId }, ct));
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
            }, ct));

        var wallet = context.Wallet ?? throw new InvalidOperationException("Wallet boundary is not configured.");
        for (var index = 0; index < deleted.Count; index++)
        {
            var bet = deleted[index];
            var result = await wallet.ApplyBatchAsync(
                bet.UserId,
                bet.ChatId,
                [new WalletBatchEffect(
                    WalletBatchEffectKind.Credit,
                    bet.Amount,
                    $"admin.clearbets.{bet.GameId}")],
                $"{context.Action}:clearbets:{index}:{bet.UserId}:{bet.ChatId}",
                ct);
            if (!result.Applied)
                throw new InvalidOperationException($"Wallet {bet.UserId}:{bet.ChatId} rejected a pending bet refund.");
        }

        context.SetOutput("bets", deleted);
    }
}
