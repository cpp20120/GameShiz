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

internal sealed class MetaQuestCatalogReloadAdminEffectHandler(
    IQuestCatalog catalog) : MetaAdminEffectHandler<MetaQuestCatalogReloadAdminEffect>
{
    protected override Task ApplyAsync(MetaQuestCatalogReloadAdminEffect effect, IAdminExecutionContext context, CancellationToken ct)
    {
        if (catalog is JsonQuestCatalog jsonCatalog)
            jsonCatalog.Reload();
        return Task.CompletedTask;
    }
}
