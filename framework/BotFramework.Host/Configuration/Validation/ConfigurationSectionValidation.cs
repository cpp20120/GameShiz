using System.Text.Json.Nodes;
using BotFramework.Sdk.Configuration;

namespace BotFramework.Host.Configuration.Validation;

internal sealed record ConfigurationSectionValidation(
    JsonNode? Effective,
    IReadOnlyList<ConfigurationValidationIssue> Issues);
