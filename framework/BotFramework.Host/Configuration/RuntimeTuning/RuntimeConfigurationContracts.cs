using BotFramework.Sdk.Configuration;

namespace BotFramework.Host.Configuration.RuntimeTuning;

public sealed record RuntimeConfigurationSnapshot(
    string PatchJson,
    string EffectiveJson,
    IReadOnlyList<ConfigurationValidationIssue> Issues);

public sealed record RuntimeConfigurationValidation(
    bool IsValid,
    string NormalizedPatchJson,
    string EffectiveJson,
    IReadOnlyList<ConfigurationValidationIssue> Issues);

public sealed record RuntimeConfigurationApplyResult(
    bool Applied,
    string PatchJson,
    string EffectiveJson,
    IReadOnlyList<ConfigurationValidationIssue> Issues);

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
