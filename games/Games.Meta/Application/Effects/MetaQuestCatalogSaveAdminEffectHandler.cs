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

internal sealed class MetaQuestCatalogSaveAdminEffectHandler : MetaAdminEffectHandler<MetaQuestCatalogSaveAdminEffect>
{
    protected override async Task ApplyAsync(MetaQuestCatalogSaveAdminEffect effect, IAdminExecutionContext context, CancellationToken ct)
    {
        var path = JsonQuestCatalog.EditablePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, effect.FormattedJson, ct);
            File.Move(tempPath, path, overwrite: true);
            context.SetOutput("path", path);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
