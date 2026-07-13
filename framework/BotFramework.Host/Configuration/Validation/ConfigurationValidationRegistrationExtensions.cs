using BotFramework.Sdk.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BotFramework.Host.Configuration.Validation;

internal static class ConfigurationValidationRegistrationExtensions
{
    public static IServiceCollection AddRegisteredConfigurationSection<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionPath)
        where TOptions : class
    {
        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionPath))
            .ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<TOptions>, OptionsValidationBridge<TOptions>>());
        services.AddSingleton<IRegisteredConfigurationSection>(provider =>
            new RegisteredConfigurationSection<TOptions>(
                sectionPath,
                configuration,
                provider.GetServices<IConfigurationValidator<TOptions>>()));
        return services;
    }

    public static IServiceCollection AddRegisteredConfigurationSection<TOptions, TValidator>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionPath)
        where TOptions : class
        where TValidator : class, IConfigurationValidator<TOptions>
    {
        services.AddSingleton<IConfigurationValidator<TOptions>, TValidator>();
        return services.AddRegisteredConfigurationSection<TOptions>(configuration, sectionPath);
    }
}
