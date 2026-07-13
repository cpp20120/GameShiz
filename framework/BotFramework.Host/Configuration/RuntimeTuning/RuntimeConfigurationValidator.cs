using System.Text.Json;
using System.Text.Json.Nodes;
using BotFramework.Host.Configuration.Validation;
using BotFramework.Sdk.Configuration;

namespace BotFramework.Host.Configuration.RuntimeTuning;

internal sealed class RuntimeConfigurationValidator(
    IEnumerable<IRegisteredConfigurationSection> registeredSections)
{
    private static readonly JsonSerializerOptions CompactJson = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions IndentedJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly IRegisteredConfigurationSection[] sections = registeredSections
        .Where(section => RuntimeTuningPayloadSanitizer.IsAllowedSectionPath(section.SectionPath))
        .GroupBy(section => section.SectionPath, StringComparer.Ordinal)
        .Select(static group => group.First())
        .OrderBy(section => section.SectionPath, StringComparer.Ordinal)
        .ToArray();

    public RuntimeConfigurationValidation Validate(string patchJson)
    {
        JsonObject patch;
        try
        {
            patch = JsonNode.Parse(patchJson) as JsonObject
                ?? throw new JsonException("Runtime configuration payload must be a JSON object.");
        }
        catch (JsonException exception)
        {
            return Invalid(new ConfigurationValidationIssue("$", "invalid_json", exception.Message));
        }

        var issues = ValidateShape(patch);
        var effective = new JsonObject();
        foreach (var section in sections)
        {
            var fragment = Navigate(patch, section.SectionPath);
            var validation = section.Validate(fragment);
            issues.AddRange(validation.Issues);
            if (validation.Effective is not null)
                SetPath(effective, section.SectionPath, validation.Effective.DeepClone());
        }

        var normalized = RuntimeTuningPayloadSanitizer.Sanitize(patch);
        return new RuntimeConfigurationValidation(
            issues.Count == 0,
            normalized.ToJsonString(CompactJson),
            effective.ToJsonString(IndentedJson),
            issues);
    }

    private List<ConfigurationValidationIssue> ValidateShape(JsonObject patch)
    {
        var registered = sections.Select(section => section.SectionPath).ToHashSet(StringComparer.Ordinal);
        var issues = new List<ConfigurationValidationIssue>();
        foreach (var rootProperty in patch)
        {
            if (!string.Equals(rootProperty.Key, "Bot", StringComparison.Ordinal)
                && !string.Equals(rootProperty.Key, "Games", StringComparison.Ordinal))
            {
                issues.Add(new(rootProperty.Key, "unknown_root_section", "Only Bot and Games runtime configuration roots are supported."));
                continue;
            }

            if (rootProperty.Value is not JsonObject root)
            {
                issues.Add(new(rootProperty.Key, "section_must_be_object", "Configuration section must be a JSON object."));
                continue;
            }

            foreach (var sectionProperty in root)
            {
                var path = $"{rootProperty.Key}:{sectionProperty.Key}";
                if (!registered.Contains(path))
                {
                    issues.Add(new(path, "unknown_section", "This runtime configuration section is not registered or is not admin-tunable."));
                    continue;
                }

                if (sectionProperty.Value is not JsonObject)
                    issues.Add(new(path, "section_must_be_object", "Configuration section must be a JSON object."));
            }
        }
        return issues;
    }

    private static JsonNode? Navigate(JsonObject root, string sectionPath)
    {
        JsonNode? current = root;
        foreach (var segment in sectionPath.Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current is not JsonObject obj || !obj.TryGetPropertyValue(segment, out current))
                return null;
        }
        return current;
    }

    private static void SetPath(JsonObject root, string sectionPath, JsonNode value)
    {
        var parts = sectionPath.Split(':', StringSplitOptions.RemoveEmptyEntries);
        JsonObject current = root;
        for (var index = 0; index < parts.Length - 1; index++)
        {
            if (current[parts[index]] is not JsonObject child)
            {
                child = new JsonObject();
                current[parts[index]] = child;
            }
            current = child;
        }
        current[parts[^1]] = value;
    }

    private static RuntimeConfigurationValidation Invalid(ConfigurationValidationIssue issue) =>
        new(false, "{}", "{}", [issue]);
}
