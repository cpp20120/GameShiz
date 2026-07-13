using System.Text.Json;
using System.Text.Json.Nodes;
using BotFramework.Host.Configuration.RuntimeTuning;
using BotFramework.Sdk.Configuration;
using Microsoft.Extensions.Configuration;

namespace BotFramework.Host.Configuration.Validation;

internal interface IRegisteredConfigurationSection
{
    string SectionPath { get; }
    Type OptionsType { get; }
    ConfigurationSectionValidation Validate(JsonNode? patch);
    JsonNode Effective(JsonNode? patch);
}

internal sealed record ConfigurationSectionValidation(
    JsonNode? Effective,
    IReadOnlyList<ConfigurationValidationIssue> Issues);

internal sealed class RegisteredConfigurationSection<TOptions>(
    string sectionPath,
    IConfiguration configuration,
    IEnumerable<IConfigurationValidator<TOptions>> validators) : IRegisteredConfigurationSection
    where TOptions : class
{
    private readonly IConfigurationValidator<TOptions>[] validators = validators.ToArray();

    public string SectionPath { get; } = sectionPath;
    public Type OptionsType => typeof(TOptions);

    public ConfigurationSectionValidation Validate(JsonNode? patch)
    {
        TOptions effective;
        try
        {
            effective = RuntimeTuningMerge.MergeSectionStrict<TOptions>(configuration, SectionPath, patch);
        }
        catch (JsonException exception)
        {
            return new(null,
            [
                new ConfigurationValidationIssue(
                    SectionPath,
                    "invalid_json_shape",
                    exception.Message),
            ]);
        }

        var issues = validators
            .SelectMany(validator => validator.Validate(effective).Issues)
            .Select(issue => issue with
            {
                Path = string.IsNullOrWhiteSpace(issue.Path)
                    ? SectionPath
                    : $"{SectionPath}:{issue.Path}",
            })
            .ToArray();
        return new(JsonSerializer.SerializeToNode(effective), issues);
    }

    public JsonNode Effective(JsonNode? patch) =>
        JsonSerializer.SerializeToNode(
            RuntimeTuningMerge.MergeSectionStrict<TOptions>(configuration, SectionPath, patch))!;
}
