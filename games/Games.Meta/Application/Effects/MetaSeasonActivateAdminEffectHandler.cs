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

internal sealed class MetaSeasonActivateAdminEffectHandler : MetaAdminEffectHandler<MetaSeasonActivateAdminEffect>
{
    protected override async Task ApplyAsync(MetaSeasonActivateAdminEffect effect, IAdminExecutionContext context, CancellationToken ct)
    {
        await context.ExecuteAsync("UPDATE meta_seasons SET status = 'finished', updated_at = now() WHERE status = 'active'", null, ct);
        var changed = await context.ExecuteAsync(
            "UPDATE meta_seasons SET status = 'active', starts_at = LEAST(starts_at, now()), updated_at = now() WHERE id = @seasonId AND status IN ('planned', 'finished')",
            new { effect.SeasonId }, ct);
        context.SetOutput("changed", changed);
        if (changed > 0)
            await AppendHistoryAsync(context, "season.activated", "season", effect.SeasonId.ToString(CultureInfo.InvariantCulture), effect.SeasonId,
                new { effect.SeasonId }, ct);
    }
}
