using Telegram.Bot.Types;
using System.Collections.Concurrent;
using System.Diagnostics;
using BotFramework.Host.Analytics;

namespace BotFramework.Host.Pipeline.Middleware;

public sealed class UpdateAnalyticsMiddleware(IAnalyticsService analytics) : IUpdateMiddleware
{
    private static readonly TimeSpan SessionIdleTimeout = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<(long UserId, long ChatId), SessionState> _sessions = new();

    public async Task InvokeAsync(UpdateContext ctx, UpdateDelegate next)
    {
        var startedAt = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var correlationId = $"telegram:{ctx.Update.Id}";
        var sessionId = ResolveSession(ctx.UserId, ctx.ChatId, startedAt);
        var previousAnalyticsContext = AnalyticsContextAccessor.Current;
        var previousRequestContext = RequestContextAccessor.Current;
        var contextTags = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["correlation_id"] = correlationId,
            ["session_id"] = sessionId,
            ["update_id"] = ctx.Update.Id,
            ["user_id"] = ctx.UserId,
            ["chat_id"] = ctx.ChatId,
            ["occurred_at"] = startedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["source"] = "telegram",
        };
        AnalyticsContextAccessor.Current = contextTags;
        RequestContextAccessor.Current = new RequestContext(
            ctx.UserId,
            ctx.Update.Message?.From?.LanguageCode ?? "ru",
            correlationId,
            contextTags.Where(x => x.Value is not null)
                .ToDictionary(x => x.Key, x => x.Value!.ToString()!, StringComparer.Ordinal));

        var outcome = "ok";
        string? errorCode = null;
        try
        {
            Track(ctx.Update, ctx.UserId, ctx.ChatId);
            await next(ctx);
        }
        catch (Exception ex)
        {
            outcome = "error";
            errorCode = ex.GetType().Name;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            analytics.Track("telegram", "update_completed", new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["kind"] = Kind(ctx.Update),
                ["outcome"] = outcome,
                ["error_code"] = errorCode,
                ["duration_ms"] = stopwatch.Elapsed.TotalMilliseconds,
            });
            AnalyticsContextAccessor.Current = previousAnalyticsContext;
            RequestContextAccessor.Current = previousRequestContext;
        }
    }

    private string ResolveSession(long userId, long chatId, DateTime now)
    {
        var key = (userId, chatId);
        var state = _sessions.AddOrUpdate(
            key,
            _ => new SessionState(Guid.NewGuid().ToString("N"), now),
            (_, current) => now - current.LastSeenAt >= SessionIdleTimeout
                ? new SessionState(Guid.NewGuid().ToString("N"), now)
                : current with { LastSeenAt = now });

        if (_sessions.Count > 10_000)
        {
            var cutoff = now - SessionIdleTimeout - SessionIdleTimeout;
            foreach (var candidate in _sessions.Where(x => x.Value.LastSeenAt < cutoff).Take(1_000))
                _sessions.TryRemove(candidate.Key, out _);
        }
        return state.Id;
    }

    private sealed record SessionState(string Id, DateTime LastSeenAt);

    private void Track(Update update, long userId, long chatId)
    {
        var kind = Kind(update);
        var tags = new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["update_id"] = update.Id,
            ["user_id"] = userId,
            ["chat_id"] = chatId,
            ["kind"] = kind,
            ["chat_type"] = ChatType(update),
        };

        switch (update)
        {
            case { Message: { Text: { } text } }:
                if (TryCommandToken(text) is { } command)
                {
                    tags["command"] = command;
                    tags["has_args"] = HasArgs(text);
                    if (string.Equals(command, "start", StringComparison.Ordinal) && TryFirstArgument(text) is { } source)
                        tags["acquisition_source"] = source;
                    analytics.Track("telegram", "command", tags);
                    return;
                }

                tags["text_length"] = text.Length;
                analytics.Track("telegram", "message", tags);
                return;

            case { Message.Dice: { } dice }:
                tags["emoji"] = dice.Emoji;
                tags["value"] = dice.Value;
                analytics.Track("telegram", "dice", tags);
                return;

            case { CallbackQuery: { } callback }:
                var data = callback.Data ?? "";
                tags["callback_prefix"] = CallbackPrefix(data);
                tags["callback_length"] = data.Length;
                tags["message_chat_id"] = callback.Message?.Chat.Id;
                analytics.Track("telegram", "callback", tags);
                return;

            default:
                analytics.Track("telegram", "update", tags);
                return;
        }
    }

    private static string Kind(Update update) => update switch
    {
        { Message.Text: not null } => "text",
        { Message.Dice: not null } => "dice",
        { CallbackQuery: not null } => "callback",
        { ChannelPost: not null } => "channel_post",
        { EditedMessage: not null } => "edited_message",
        { InlineQuery: not null } => "inline_query",
        _ => update.Type.ToString().ToLowerInvariant(),
    };

    private static string? ChatType(Update update)
    {
        var chat = update.Message?.Chat
            ?? update.EditedMessage?.Chat
            ?? update.ChannelPost?.Chat
            ?? update.CallbackQuery?.Message?.Chat;
        return chat?.Type.ToString().ToLowerInvariant();
    }

    private static string? TryCommandToken(string text)
    {
        var span = text.AsSpan().TrimStart();
        if (span.IsEmpty || span[0] != '/')
            return null;

        var spaceIndex = span.IndexOf(' ');
        var token = spaceIndex >= 0 ? span[..spaceIndex] : span;
        var mentionIndex = token.IndexOf('@');
        if (mentionIndex >= 0)
            token = token[..mentionIndex];

        return token.TrimStart('/').ToString().ToLowerInvariant();
    }

    private static bool HasArgs(string text)
    {
        var span = text.AsSpan().Trim();
        var spaceIndex = span.IndexOf(' ');
        return spaceIndex >= 0 && spaceIndex < span.Length - 1;
    }

    private static string? TryFirstArgument(string text)
    {
        var parts = text.AsSpan().Trim().ToString().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;
        var value = new string(parts[1].Trim()
            .Where(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-')
            .Take(64)
            .ToArray());
        return value.Length == 0 ? null : value;
    }

    private static string CallbackPrefix(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
            return "empty";

        var separators = new[] { ':', '|', ';', ' ' };
        var index = data.IndexOfAny(separators);
        var prefix = index >= 0 ? data[..index] : data;
        return prefix.Length <= 48 ? prefix : prefix[..48];
    }
}
