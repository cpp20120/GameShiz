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

internal sealed class MetaSeasonCreateAdminEffectHandler : MetaAdminEffectHandler<MetaSeasonCreateAdminEffect>
{
    protected override async Task ApplyAsync(MetaSeasonCreateAdminEffect effect, IAdminExecutionContext context, CancellationToken ct)
    {
        var id = await context.QuerySingleOrDefaultAsync<long?>(
            """
            INSERT INTO meta_seasons (name, starts_at, ends_at, status, config)
            VALUES (@name, @startsAt, @endsAt, 'planned', CAST(@configJson AS jsonb))
            RETURNING id
            """,
            new { name = effect.Name, startsAt = effect.StartsAt, endsAt = effect.EndsAt, configJson = effect.ConfigJson }, ct)
            ?? throw new InvalidOperationException("Season creation did not return an id.");
        context.SetOutput("seasonId", id);
        await AppendHistoryAsync(context, "season.created", "season", id.ToString(CultureInfo.InvariantCulture), id,
            new { id, effect.Name, effect.StartsAt, effect.EndsAt }, ct);
    }
}
