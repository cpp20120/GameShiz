using Dapper;
using BotFramework.Contracts.Identity;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace BotFramework.Host.Pipeline.Middleware;

public sealed partial class KnownChatsMiddleware(
    INpgsqlConnectionFactory connections,
    ILogger<KnownChatsMiddleware> logger,
    IPlayerDirectory? players = null) : IUpdateMiddleware
{
    public async Task InvokeAsync(UpdateContext ctx, UpdateDelegate next)
    {
        await TryRecordChatAsync(ctx.Update, ctx.Ct);
        await TryRecordPlayerAsync(ctx.Update, ctx.Ct);
        await next(ctx);
    }

    private async Task TryRecordPlayerAsync(Update update, CancellationToken ct)
    {
        var user = update.Message?.From
            ?? update.EditedMessage?.From
            ?? update.CallbackQuery?.From
            ?? update.MyChatMember?.From
            ?? update.ChatMember?.From
            ?? update.ChatJoinRequest?.From;
        if (players is null || user is null || user.Id == 0 || user.IsBot) return;

        var displayName = string.Join(' ', new[] { user.FirstName, user.LastName }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(displayName)) displayName = user.Username ?? $"User {user.Id}";
        var now = DateTimeOffset.UtcNow;
        try
        {
            await players.UpsertAsync(new PlayerIdentity(user.Id, displayName, user.Username, now, now), ct);
        }
        catch (Exception ex)
        {
            LogPlayerUpsertFailed(user.Id, ex);
        }
    }

    private async Task TryRecordChatAsync(Update u, CancellationToken ct)
    {
        var chat = GetChat(u);
        if (chat is null) return;
        if (chat.Id == 0) return;

        var title = BuildTitle(chat);
        var type = chat.Type.ToString().ToLowerInvariant();
        var username = chat.Username;

        try
        {
            await using var conn = await connections.OpenAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO known_chats (chat_id, chat_type, title, username, first_seen_at, last_seen_at)
                VALUES (@id, @type, @title, @username, now(), now())
                ON CONFLICT (chat_id) DO UPDATE SET
                    chat_type    = EXCLUDED.chat_type,
                    title        = COALESCE(EXCLUDED.title, known_chats.title),
                    username     = COALESCE(EXCLUDED.username, known_chats.username),
                    last_seen_at = now()
                """,
                new { id = chat.Id, type, title, username },
                cancellationToken: ct));
        }
        catch (Exception ex)
        {
            LogKnownChatUpsertFailed(chat.Id, ex);
        }
    }

    private static string? BuildTitle(Chat chat) =>
        chat.Type == ChatType.Private
            ? (string.IsNullOrWhiteSpace(chat.FirstName) && string.IsNullOrWhiteSpace(chat.LastName)
                ? null
                : string.Join(' ', new[] { chat.FirstName, chat.LastName }
                    .Where(s => !string.IsNullOrWhiteSpace(s))))
            : chat.Title ?? chat.Username;

    private static Chat? GetChat(Update u) =>
        u.Message?.Chat
        ?? u.EditedMessage?.Chat
        ?? u.ChannelPost?.Chat
        ?? u.CallbackQuery?.Message?.Chat
        ?? u.MyChatMember?.Chat
        ?? u.ChatMember?.Chat
        ?? u.ChatJoinRequest?.Chat;

    [LoggerMessage(EventId = 1500, Level = LogLevel.Debug, Message = "known_chats.upsert_failed chat_id={ChatId}")]
    private partial void LogKnownChatUpsertFailed(long chatId, Exception ex);

    [LoggerMessage(EventId = 1501, Level = LogLevel.Debug, Message = "player_identity.upsert_failed user_id={UserId}")]
    private partial void LogPlayerUpsertFailed(long userId, Exception ex);
}
