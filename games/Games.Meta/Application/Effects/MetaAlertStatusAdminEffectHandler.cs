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

internal sealed class MetaAlertStatusAdminEffectHandler : MetaAdminEffectHandler<MetaAlertStatusAdminEffect>
{
    protected override async Task ApplyAsync(MetaAlertStatusAdminEffect effect, IAdminExecutionContext context, CancellationToken ct)
    {
        var changed = await context.ExecuteAsync(
            """
            UPDATE meta_risk_flags
            SET status = @targetStatus, resolved_at = now(), updated_at = now()
            WHERE id = @flagId AND status = 'open'
            """,
            new { flagId = effect.FlagId, effect.TargetStatus }, ct);
        context.SetOutput("changed", changed);
        if (changed > 0)
            await AppendHistoryAsync(context, "meta_alert.updated", "risk_flag", effect.FlagId.ToString(CultureInfo.InvariantCulture), null,
                new { effect.FlagId, effect.TargetStatus }, ct);
    }
}
