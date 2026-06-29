using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Games.Meta.Application.Meta;

[Command("/mystats")]
public sealed class MyStatsHandler(
    IPlayerProtectionService protection,
    IAnalyticsService analytics) : IUpdateHandler
{
    public async Task HandleAsync(UpdateContext ctx)
    {
        var msg = ctx.Update.Message;
        var userId = msg?.From?.Id ?? 0;
        if (msg?.Text is null || userId == 0) return;
        if (msg.Chat.Type != ChatType.Private)
        {
            await ctx.Bot.SendMessage(msg.Chat.Id, "🔒 <code>/mystats</code> доступна только в личном чате с ботом.",
                parseMode: ParseMode.Html,
                replyParameters: new ReplyParameters { MessageId = msg.MessageId },
                cancellationToken: ctx.Ct);
            return;
        }

        var parts = msg.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var action = parts.Length > 1 ? parts[1].ToLowerInvariant() : "show";
        var response = action switch
        {
            "limit" => await SetLimitAsync(userId, parts, ctx.Ct),
            "cooldown" => await SetCooldownAsync(userId, parts, ctx.Ct),
            "exclude" => await SetExclusionAsync(userId, parts, ctx.Ct),
            "show" => await RenderStatsAsync(userId, msg.Chat.Id, ctx.Ct),
            _ => Usage,
        };

        analytics.Track("responsible_gaming", "settings_viewed", new Dictionary<string, object?>
        {
            ["user_id"] = userId,
            ["chat_id"] = msg.Chat.Id,
            ["action"] = action,
        });
        await ctx.Bot.SendMessage(msg.Chat.Id, response,
            parseMode: ParseMode.Html,
            replyParameters: new ReplyParameters { MessageId = msg.MessageId },
            cancellationToken: ctx.Ct);
    }

    private async Task<string> RenderStatsAsync(long userId, long scopeId, CancellationToken ct)
    {
        var s = await protection.GetStatsAsync(userId, scopeId, ct);
        var net7 = s.Payout7Days - s.Stake7Days;
        var net30 = s.Payout30Days - s.Stake30Days;
        var limit = s.DailyStakeLimit is { } value
            ? string.Create(CultureInfo.InvariantCulture, $"{s.StakeToday}/{value} монет (UTC)")
            : "не установлен";
        var cooldown = ActiveUntil(s.CooldownUntil);
        var excluded = ActiveUntil(s.SelfExcludedUntil);

        return string.Create(CultureInfo.InvariantCulture, $"""
            📊 <b>Моя статистика</b>

            Баланс в этом чате: <b>{s.Balance}</b>
            7 дней: ставки <b>{s.Stake7Days}</b> · выплаты <b>{s.Payout7Days}</b> · итог <b>{Signed(net7)}</b>
            30 дней: ставки <b>{s.Stake30Days}</b> · выплаты <b>{s.Payout30Days}</b> · итог <b>{Signed(net30)}</b>

            🛡 Дневной лимит: <b>{limit}</b>
            ⏸ Перерыв: <b>{cooldown}</b>
            🚫 Самоисключение: <b>{excluded}</b>

            <code>/mystats limit 500</code> — дневной лимит
            <code>/mystats limit off</code> — убрать лимит
            <code>/mystats cooldown 24h</code> — перерыв (1h–30d)
            <code>/mystats exclude 30d</code> — самоисключение (7d–3650d)

            <i>Активный перерыв и самоисключение нельзя отменить досрочно.</i>
            """);
    }

    private async Task<string> SetLimitAsync(long userId, string[] parts, CancellationToken ct)
    {
        if (parts.Length != 3) return Usage;
        if (string.Equals(parts[2], "off", StringComparison.OrdinalIgnoreCase))
        {
            await protection.SetDailyLimitAsync(userId, null, ct);
            return "🛡 Дневной лимит отключён.";
        }
        if (!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var limit) || limit < 0)
            return "Укажи целое число от 0 или <code>off</code>.";

        await protection.SetDailyLimitAsync(userId, limit, ct);
        return string.Create(CultureInfo.InvariantCulture, $"🛡 Дневной лимит установлен: <b>{limit}</b> монет (UTC)." );
    }

    private async Task<string> SetCooldownAsync(long userId, string[] parts, CancellationToken ct)
    {
        if (parts.Length != 3 || !TryDuration(parts[2], out var duration) ||
            duration < TimeSpan.FromHours(1) || duration > TimeSpan.FromDays(30))
            return "Допустимый перерыв: от <code>1h</code> до <code>30d</code>.";

        var until = DateTimeOffset.UtcNow.Add(duration);
        await protection.SetCooldownAsync(userId, until, ct);
        return string.Create(CultureInfo.InvariantCulture, $"⏸ Перерыв активен до <code>{until:yyyy-MM-dd HH:mm} UTC</code>." );
    }

    private async Task<string> SetExclusionAsync(long userId, string[] parts, CancellationToken ct)
    {
        if (parts.Length != 3 || !TryDuration(parts[2], out var duration) ||
            duration < TimeSpan.FromDays(7) || duration > TimeSpan.FromDays(3650))
            return "Самоисключение: от <code>7d</code> до <code>3650d</code>.";

        var until = DateTimeOffset.UtcNow.Add(duration);
        await protection.SetSelfExclusionAsync(userId, until, ct);
        return string.Create(CultureInfo.InvariantCulture, $"🚫 Самоисключение активно до <code>{until:yyyy-MM-dd HH:mm} UTC</code>. Досрочная отмена недоступна." );
    }

    private static bool TryDuration(string value, out TimeSpan duration)
    {
        duration = default;
        if (value.Length < 2 || !int.TryParse(value[..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var amount))
            return false;
        duration = char.ToLowerInvariant(value[^1]) switch
        {
            'h' => TimeSpan.FromHours(amount),
            'd' => TimeSpan.FromDays(amount),
            _ => default,
        };
        return duration > TimeSpan.Zero;
    }

    private static string ActiveUntil(DateTimeOffset? value) => value is { } until && until > DateTimeOffset.UtcNow
        ? string.Create(CultureInfo.InvariantCulture, $"до {until:yyyy-MM-dd HH:mm} UTC")
        : "нет";

    private static string Signed(long value) => value > 0
        ? string.Create(CultureInfo.InvariantCulture, $"+{value}")
        : value.ToString(CultureInfo.InvariantCulture);

    private const string Usage = "Использование: <code>/mystats</code>, <code>/mystats limit 500|off</code>, <code>/mystats cooldown 24h</code>, <code>/mystats exclude 30d</code>.";
}
