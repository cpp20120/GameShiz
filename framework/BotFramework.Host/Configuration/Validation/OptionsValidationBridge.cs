using BotFramework.Sdk.Configuration;
using Microsoft.Extensions.Options;

namespace BotFramework.Host.Configuration.Validation;

internal sealed class OptionsValidationBridge<TOptions>(
    IEnumerable<IConfigurationValidator<TOptions>> validators) : IValidateOptions<TOptions>
    where TOptions : class
{
    private readonly IConfigurationValidator<TOptions>[] validators = validators.ToArray();

    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        var failures = validators
            .SelectMany(validator => validator.Validate(options).Issues)
            .Select(static issue => $"{issue.Path}: {issue.Message} ({issue.Code})")
            .ToArray();
        return failures.Length == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
