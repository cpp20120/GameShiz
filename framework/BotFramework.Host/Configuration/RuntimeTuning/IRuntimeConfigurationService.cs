namespace BotFramework.Host.Configuration.RuntimeTuning;

public interface IRuntimeConfigurationService
{
    Task<RuntimeConfigurationSnapshot> GetAsync(CancellationToken ct);

    RuntimeConfigurationValidation Validate(string patchJson);

    Task<RuntimeConfigurationApplyResult> ApplyAsync(
        string patchJson,
        long actorId,
        string actorName,
        string auditAction,
        object? auditDetails,
        CancellationToken ct);
}
