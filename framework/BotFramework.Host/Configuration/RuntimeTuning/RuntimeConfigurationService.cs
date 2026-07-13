using BotFramework.Host.Admin.Execution;
using BotFramework.Sdk.Admin.Execution;
using Dapper;

namespace BotFramework.Host.Configuration.RuntimeTuning;

internal sealed class RuntimeConfigurationService(
    INpgsqlConnectionFactory connections,
    RuntimeConfigurationValidator validator,
    IRuntimeTuningAccessor tuning,
    IAdminEffectExecutor effects) : IRuntimeConfigurationService
{
    public async Task<RuntimeConfigurationSnapshot> GetAsync(CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct).ConfigureAwait(false);
        var stored = await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT payload::text FROM runtime_tuning WHERE id = 1",
            cancellationToken: ct)).ConfigureAwait(false);
        var validation = validator.Validate(string.IsNullOrWhiteSpace(stored) ? "{}" : stored);
        return new RuntimeConfigurationSnapshot(
            validation.NormalizedPatchJson,
            validation.EffectiveJson,
            validation.Issues);
    }

    public RuntimeConfigurationValidation Validate(string patchJson) => validator.Validate(patchJson);

    public async Task<RuntimeConfigurationApplyResult> ApplyAsync(
        string patchJson,
        long actorId,
        string actorName,
        string auditAction,
        object? auditDetails,
        CancellationToken ct)
    {
        var validation = validator.Validate(patchJson);
        if (!validation.IsValid)
            return new(false, validation.NormalizedPatchJson, validation.EffectiveJson, validation.Issues);

        await effects.ExecuteAsync(
            new AdminExecutionEnvelope(
                new AdminActor(actorId, actorName),
                auditAction,
                new
                {
                    patchBytes = validation.NormalizedPatchJson.Length,
                    payload = auditDetails,
                }),
            new AdminEffectPlan<bool>(true,
            [
                new RuntimeConfigurationPatchEffect(validation.NormalizedPatchJson),
            ]),
            ct).ConfigureAwait(false);

        await tuning.ReloadFromDatabaseAsync(ct).ConfigureAwait(false);
        return new(true, validation.NormalizedPatchJson, validation.EffectiveJson, []);
    }
}
