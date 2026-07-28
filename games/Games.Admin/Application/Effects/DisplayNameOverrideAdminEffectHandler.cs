using BotFramework.Host.Admin.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Admin.Execution;
using Dapper;
using Games.Admin.Infrastructure.Models;

namespace Games.Admin.Application.Effects;

internal sealed class DisplayNameOverrideAdminEffectHandler : AdminEffectHandler<DisplayNameOverrideAdminEffect>
{
    protected override Task ApplyAsync(
        DisplayNameOverrideAdminEffect effect,
        IAdminExecutionContext context,
        CancellationToken ct) =>
        effect.NewName is null
            ? context.ExecuteAsync(
                "DELETE FROM display_name_overrides WHERE original_name = @originalName",
                new { effect.OriginalName }, ct)
            : context.ExecuteAsync(
                """
                INSERT INTO display_name_overrides (original_name, new_name)
                VALUES (@originalName, @newName)
                ON CONFLICT (original_name) DO UPDATE SET new_name = EXCLUDED.new_name
                """,
                new { effect.OriginalName, effect.NewName }, ct);
}
