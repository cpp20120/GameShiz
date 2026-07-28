using System.Globalization;
using System.Text.Json;
using BotFramework.Host.Admin.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Admin.Execution;
using Dapper;
using Games.Meta.Application.Clans;
using Games.Meta.Application.Models;
using Games.Meta.Infrastructure.Catalog;
using Games.Meta.Domain.Seasons;

namespace Games.Meta.Application.Effects;

internal sealed class MetaSeasonPrepareAdminEffectHandler : MetaAdminEffectHandler<MetaSeasonPrepareAdminEffect>
{
    protected override async Task ApplyAsync(MetaSeasonPrepareAdminEffect effect, IAdminExecutionContext context, CancellationToken ct)
    {
        var existing = await context.QuerySingleOrDefaultAsync<int>(
            "SELECT count(*)::int FROM meta_seasons WHERE status = 'planned' AND ends_at > now()",
            null, ct);
        var toCreate = Math.Max(0, effect.RequestedCount - existing);
        var created = 0;
        if (toCreate > 0)
        {
            var startsAt = await context.QuerySingleOrDefaultAsync<DateTimeOffset>(
                "SELECT COALESCE(max(ends_at), date_trunc('day', now())) FROM meta_seasons WHERE status IN ('active', 'planned')",
                null, ct);
            var startNumber = await context.QuerySingleOrDefaultAsync<int>(
                "SELECT count(*)::int + 1 FROM meta_seasons", null, ct);
            foreach (var plan in SeasonPlanFactory.CreatePlans(startsAt, toCreate, effect.DurationDays, startNumber))
            {
                await context.ExecuteAsync(
                    """
                    INSERT INTO meta_seasons (name, starts_at, ends_at, status, config)
                    VALUES (@name, @startsAt, @endsAt, 'planned', CAST(@configJson AS jsonb))
                    """,
                    new { name = plan.Name, startsAt = plan.StartsAt, endsAt = plan.EndsAt, configJson = plan.ConfigJson }, ct);
                created++;
            }
        }

        context.SetOutput("created", created);
        context.SetOutput("existingPlanned", existing);
        await AppendHistoryAsync(context, "season.prepared", "season", "planned", null,
            new { requested = effect.RequestedCount, existingPlanned = existing, created, durationDays = effect.DurationDays }, ct);
    }
}
