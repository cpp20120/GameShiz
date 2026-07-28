namespace BotFramework.Sdk.Configuration;

/// <summary>
/// Library-neutral semantic validator for typed framework/module configuration.
/// Host adapters may bridge FluentValidation, DataAnnotations, or handwritten rules.
/// </summary>
public interface IConfigurationValidator<in TOptions>
    where TOptions : class
{
    ConfigurationValidationResult Validate(TOptions options);
}