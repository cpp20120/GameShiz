using BotFramework.Host.Admin.Execution;

namespace BotFramework.Host.Configuration.RuntimeTuning;

internal sealed class RuntimeConfigurationPatchEffectHandler
    : AdminEffectHandler<RuntimeConfigurationPatchEffect>
{
    protected override async Task ApplyAsync(
        RuntimeConfigurationPatchEffect effect,
        IAdminExecutionContext context,
        CancellationToken ct)
    {
        var affected = await context.ExecuteAsync(
            """
            UPDATE runtime_tuning
            SET payload = @payload::jsonb, updated_at = now()
            WHERE id = 1
            """,
            new { payload = effect.NormalizedPatchJson },
            ct);

        if (affected != 1)
            throw new InvalidOperationException("The runtime_tuning singleton row is missing.");
    }
}
