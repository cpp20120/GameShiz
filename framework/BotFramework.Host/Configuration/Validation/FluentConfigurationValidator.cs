using BotFramework.Sdk.Configuration;
using FluentValidation;

namespace BotFramework.Host.Configuration.Validation;

/// <summary>
/// FluentValidation adapter that still exposes the neutral SDK validator contract.
/// Game modules inherit this type and register through BindOptions&lt;T,TValidator&gt;.
/// </summary>
public abstract class FluentConfigurationValidator<TOptions> : AbstractValidator<TOptions>, IConfigurationValidator<TOptions>
    where TOptions : class
{
    ConfigurationValidationResult IConfigurationValidator<TOptions>.Validate(TOptions options)
    {
        var result = Validate(options);
        return result.IsValid
            ? ConfigurationValidationResult.Valid
            : new ConfigurationValidationResult(result.Errors
                .Select(static failure => new ConfigurationValidationIssue(
                    failure.PropertyName,
                    string.IsNullOrWhiteSpace(failure.ErrorCode) ? "invalid" : failure.ErrorCode,
                    failure.ErrorMessage))
                .ToArray());
    }
}
