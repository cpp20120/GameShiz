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

internal sealed class MetaSeasonFinishAdminEffectHandler : MetaAdminEffectHandler<MetaSeasonFinishAdminEffect>
{
    protected override async Task ApplyAsync(MetaSeasonFinishAdminEffect effect, IAdminExecutionContext context, CancellationToken ct)
    {
        var changed = await context.ExecuteAsync(
            "UPDATE meta_seasons SET status = 'finished', ends_at = LEAST(ends_at, now()), updated_at = now() WHERE id = @seasonId AND status <> 'finished'",
            new { effect.SeasonId }, ct);
        context.SetOutput("changed", changed);
        if (changed > 0)
            await AppendHistoryAsync(context, "season.finished", "season", effect.SeasonId.ToString(CultureInfo.InvariantCulture), effect.SeasonId,
                new { effect.SeasonId }, ct);
    }
}
