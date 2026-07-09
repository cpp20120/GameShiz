// ─────────────────────────────────────────────────────────────────────────────
// BlackjackHandTimeoutJob — sweeps abandoned hands every 30 seconds. An
// idle hand is auto-stood (settled on whatever the player currently has),
// releasing the UserState slot so the player can start a new hand.
//
// Ported from src/CasinoShiz.Core/Services/BlackjackHandTimeoutService.cs and
// executed every 30 seconds by Quartz after module migrations have completed.
// ─────────────────────────────────────────────────────────────────────────────

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using BotFramework.Scheduling.Abstractions;

namespace Games.Blackjack.Application.Jobs;

public sealed partial class BlackjackHandTimeoutJob(
    IServiceProvider services,
    IOptions<BlackjackOptions> options,
    ILogger<BlackjackHandTimeoutJob> logger) : IRecurringScheduledCommand
{
    private readonly BlackjackOptions _opts = options.Value;

    public string Key => "blackjack.hand_timeout";
    public ScheduleDescriptor Schedule => ScheduleDescriptor.Every(TimeSpan.FromSeconds(30));

    public async Task ExecuteAsync(IReadOnlyDictionary<string, string> data, CancellationToken ct)
    {
        try { await SweepAsync(ct); }
        catch (Exception ex) { LogSweepFailed(ex); }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        BlackjackService.PruneGates(_opts.HandTimeoutMs);

        using var scope = services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IBlackjackHandStore>();
        var service = scope.ServiceProvider.GetRequiredService<IBlackjackService>();

        var cutoff = DateTimeOffset.UtcNow.AddMilliseconds(-_opts.HandTimeoutMs);
        var stuck = await store.ListStuckUserIdsAsync(cutoff, ct);

        foreach (var userId in stuck)
        {
            try
            {
                await service.StandAsync(userId, ct);
                LogTimeoutFired(userId);
            }
            catch (Exception ex) { LogTimeoutActionFailed(userId, ex); }
        }
    }

    [LoggerMessage(LogLevel.Error, "blackjack.timeout.sweep failed")]
    partial void LogSweepFailed(Exception exception);

    [LoggerMessage(LogLevel.Information, "blackjack.timeout.fired user={UserId}")]
    partial void LogTimeoutFired(long userId);

    [LoggerMessage(LogLevel.Warning, "blackjack.timeout.action_failed user={UserId}")]
    partial void LogTimeoutActionFailed(long userId, Exception exception);
}
