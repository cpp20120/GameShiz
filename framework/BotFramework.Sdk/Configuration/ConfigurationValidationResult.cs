namespace BotFramework.Sdk.Configuration;

public sealed record ConfigurationValidationResult(
    IReadOnlyList<ConfigurationValidationIssue> Issues)
{
    public static ConfigurationValidationResult Valid { get; } = new([]);

    public bool IsValid => Issues.Count == 0;

    public static ConfigurationValidationResult Invalid(params ConfigurationValidationIssue[] issues) =>
        new(issues);
}