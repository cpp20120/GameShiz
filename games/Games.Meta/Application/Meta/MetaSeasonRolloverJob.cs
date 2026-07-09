using Dapper;
using Microsoft.Extensions.DependencyInjection;
using BotFramework.Scheduling.Abstractions;

namespace Games.Meta.Application.Meta;

public sealed partial class MetaSeasonRolloverJob(
    IServiceScopeFactory scopes,
    ILogger<MetaSeasonRolloverJob> logger) : IRecurringScheduledCommand
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    public string Key => "meta.season_rollover";
    public ScheduleDescriptor Schedule => ScheduleDescriptor.Every(Interval);

    public Task ExecuteAsync(IReadOnlyDictionary<string, string> data, CancellationToken ct) => TickAsync(ct);

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var meta = scope.ServiceProvider.GetRequiredService<IMetaStore>();
        var rewards = scope.ServiceProvider.GetRequiredService<ISeasonRewardService>();
        var connections = scope.ServiceProvider.GetRequiredService<INpgsqlConnectionFactory>();

        var active = await meta.GetOrCreateActiveSeasonAsync(ct);
        LogActive(active.Id, active.Name);

        await using var conn = await connections.OpenAsync(ct);
        var seasonIds = (await conn.QueryAsync<long>(new CommandDefinition(
            """
            SELECT id
            FROM meta_seasons
            WHERE status = 'finished'
              AND ends_at >= now() - interval '90 days'
            ORDER BY ends_at DESC, id DESC
            LIMIT 30
            """,
            cancellationToken: ct))).ToArray();

        foreach (var seasonId in seasonIds)
        {
            var player = await rewards.ProcessPlayerRewardsAsync(seasonId, ct);
            var clan = await rewards.ProcessClanRewardsAsync(seasonId, ct);
            if (player.Paid > 0 || clan.Paid > 0)
                LogRewards(seasonId, player.Paid, clan.Paid);
        }
    }

    [LoggerMessage(LogLevel.Debug, "meta.rollover.active season={SeasonId} name={Name}")]
    partial void LogActive(long seasonId, string name);

    [LoggerMessage(LogLevel.Information, "meta.rollover.rewards season={SeasonId} player_paid={PlayerPaid} clan_paid={ClanPaid}")]
    partial void LogRewards(long seasonId, int playerPaid, int clanPaid);
}
