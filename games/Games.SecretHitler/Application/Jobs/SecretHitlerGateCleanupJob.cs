
using BotFramework.Scheduling.Abstractions;

namespace Games.SecretHitler.Application.Jobs;

public sealed partial class SecretHitlerGateCleanupJob : IRecurringScheduledCommand
{
    private const long IdleMs = 60 * 60 * 1_000; // 1 hour

    public string Key => "sh.gate_cleanup";
    public ScheduleDescriptor Schedule => ScheduleDescriptor.Every(TimeSpan.FromMinutes(10));

    public Task ExecuteAsync(IReadOnlyDictionary<string, string> data, CancellationToken ct)
    {
        SecretHitlerService.PruneGates(IdleMs);
        return Task.CompletedTask;
    }
}
