using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CasinoShiz.Host.Debug;

[Command("/__debug_jobs")]
public sealed class DebugJobsHandler(
    IBackgroundJobStatusService jobs,
    IConfiguration configuration,
    IOptions<BotFrameworkOptions> botOptions) : IUpdateHandler
{
    public async Task HandleAsync(UpdateContext ctx)
    {
        var msg = ctx.Update.Message;
        if (msg?.Text is null) return;

        var enabled = configuration.GetValue("Debug:Enabled", defaultValue: true);
        if (!enabled) return;

        if (configuration.GetValue("Debug:RequireAdmin", defaultValue: true))
        {
            var userId = msg.From?.Id ?? 0;
            if (!IsAnyAdmin(botOptions.Value, userId))
            {
                await ctx.Bot.SendMessage(
                    msg.Chat.Id,
                    "🚫 <b>Debug jobs</b>: not allowed.",
                    parseMode: ParseMode.Html,
                    replyParameters: new ReplyParameters { MessageId = msg.MessageId },
                    cancellationToken: ctx.Ct);
                return;
            }
        }

        var snapshots = jobs.Snapshot();
        var sb = new StringBuilder();
        sb.AppendLine("<b>Background / host jobs</b>");
        if (snapshots.Count == 0)
        {
            sb.AppendLine("No jobs registered.");
        }
        else
        {
            foreach (var job in snapshots)
            {
                sb.AppendLine();
                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"<b>{Enc(job.Name)}</b> <code>{Enc(job.Kind)}</code>");
                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"state: <code>{Enc(job.State)}</code>");
                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"heartbeat: <code>{Fmt(job.LastHeartbeatAt)}</code>");
                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"started: <code>{Fmt(job.LastStartedAt)}</code>");
                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"completed: <code>{Fmt(job.LastCompletedAt)}</code>");
                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"failed: <code>{Fmt(job.LastFailedAt)}</code>");
                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"next: <code>{Fmt(job.NextRunAt)}</code>");
                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"crashes: <code>{job.CrashCount}</code>");
                if (job.RestartBackoffMs.HasValue)
                    sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"restart backoff: <code>{job.RestartBackoffMs.Value}ms</code>");
                if (!string.IsNullOrWhiteSpace(job.Note))
                    sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"note: <code>{Enc(job.Note)}</code>");
                if (!string.IsNullOrWhiteSpace(job.LastError))
                    sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"error: <code>{Enc(job.LastError)}</code>");
            }
        }

        await ctx.Bot.SendMessage(
            msg.Chat.Id,
            sb.ToString().TrimEnd(),
            parseMode: ParseMode.Html,
            replyParameters: new ReplyParameters { MessageId = msg.MessageId },
            cancellationToken: ctx.Ct);
    }

    private static bool IsAnyAdmin(BotFrameworkOptions o, long userId)
    {
        return o.Admins.Contains(userId) || o.ReadOnlyAdmins.Contains(userId);
    }

    private static string Fmt(DateTimeOffset? value) =>
        value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) ?? "—";

    private static string Enc<T>(T value) => WebUtility.HtmlEncode(value?.ToString() ?? "");
}
