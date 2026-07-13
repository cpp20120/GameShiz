// ─────────────────────────────────────────────────────────────────────────────
// PokerTurnTimeoutJob — sweeps tables whose current seat hasn't acted within
// PokerOptions.TurnTimeoutMs. Each idle table gets an auto-action (check if
// nothing to call, otherwise fold) applied and a notification broadcast.
//
// Ported from src/CasinoShiz.Core/Services/PokerTurnTimeoutService.cs and
// scheduled every 10 seconds by Quartz after module migrations have completed.
// ─────────────────────────────────────────────────────────────────────────────

using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using BotFramework.Scheduling.Abstractions;

namespace Games.Poker.Application.Jobs;

public sealed partial class PokerTurnTimeoutJob(
    IServiceProvider services,
    IRuntimeTuningAccessor tuning,
    ILogger<PokerTurnTimeoutJob> logger) : IRecurringScheduledCommand
{
    public string Key => "poker.turn_timeout";
    public ScheduleDescriptor Schedule => ScheduleDescriptor.Every(TimeSpan.FromSeconds(10));

    public async Task ExecuteAsync(IReadOnlyDictionary<string, string> data, CancellationToken ct)
    {
        try { await SweepAsync(ct); }
        catch (Exception ex) { LogPokerTimeoutSweepFailed(ex); }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        var opts = tuning.GetSection<PokerOptions>(PokerOptions.SectionName);
        using var scope = services.CreateScope();
        var bot = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
        var service = scope.ServiceProvider.GetRequiredService<IPokerService>();
        var handler = scope.ServiceProvider.GetRequiredService<PokerHandler>();

        var cutoff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - opts.TurnTimeoutMs;
        var stuck = await service.ListStuckCodesAsync(cutoff, ct);

        foreach (var code in stuck)
        {
            try
            {
                var result = await service.RunAutoActionAsync(code, ct);
                if (result == null) continue;
                LogPokerTimeoutFired(code, result.Transition);
                await handler.BroadcastAutoActionAsync(bot, result, ct);
            }
            catch (Exception ex)
            {
                LogPokerTimeoutActionFailed(code, ex);
            }
        }
    }

    [LoggerMessage(LogLevel.Error, "poker.timeout.sweep failed")]
    partial void LogPokerTimeoutSweepFailed(Exception exception);

    [LoggerMessage(LogLevel.Information, "poker.timeout.fired code={Code} transition={t}")]
    partial void LogPokerTimeoutFired(string code, HandTransition t);

    [LoggerMessage(LogLevel.Warning, "poker.timeout.action_failed code={Code}")]
    partial void LogPokerTimeoutActionFailed(string code, Exception exception);
}
