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

internal sealed class MetaSeasonConfigAdminEffectHandler : MetaAdminEffectHandler<MetaSeasonConfigAdminEffect>
{
    protected override async Task ApplyAsync(MetaSeasonConfigAdminEffect effect, IAdminExecutionContext context, CancellationToken ct)
    {
        var changed = await context.ExecuteAsync(
            "UPDATE meta_seasons SET config = CAST(@configJson AS jsonb), updated_at = now() WHERE id = @seasonId",
            new { seasonId = effect.SeasonId, effect.ConfigJson }, ct);
        context.SetOutput("changed", changed);
        if (changed > 0)
            await AppendHistoryAsync(context, "season.config_updated", "season", effect.SeasonId.ToString(CultureInfo.InvariantCulture), effect.SeasonId,
                new { effect.SeasonId, structured = effect.Structured }, ct);
    }
}
