using BotFramework.Sdk.Configuration;

namespace BotFramework.Host.Configuration.RuntimeTuning;

public sealed record RuntimeConfigurationValidation(
    bool IsValid,
    string NormalizedPatchJson,
    string EffectiveJson,
    IReadOnlyList<ConfigurationValidationIssue> Issues);
