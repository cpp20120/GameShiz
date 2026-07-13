namespace BotFramework.Sdk.Configuration;

public sealed record ConfigurationValidationIssue(
    string Path,
    string Code,
    string Message);

public sealed record ConfigurationValidationResult(
    IReadOnlyList<ConfigurationValidationIssue> Issues)
{
    public static ConfigurationValidationResult Valid { get; } = new([]);

    public bool IsValid => Issues.Count == 0;

    public static ConfigurationValidationResult Invalid(params ConfigurationValidationIssue[] issues) =>
        new(issues);
}

/// <summary>
/// Library-neutral semantic validator for typed framework/module configuration.
/// Host adapters may bridge FluentValidation, DataAnnotations, or handwritten rules.
/// </summary>
public interface IConfigurationValidator<in TOptions>
    where TOptions : class
{
    ConfigurationValidationResult Validate(TOptions options);
}
