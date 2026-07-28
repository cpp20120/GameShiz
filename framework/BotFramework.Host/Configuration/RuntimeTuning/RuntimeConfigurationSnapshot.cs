using BotFramework.Sdk.Configuration;

namespace BotFramework.Host.Configuration.RuntimeTuning;

public sealed record RuntimeConfigurationSnapshot(
    string PatchJson,
    string EffectiveJson,
    IReadOnlyList<ConfigurationValidationIssue> Issues);
