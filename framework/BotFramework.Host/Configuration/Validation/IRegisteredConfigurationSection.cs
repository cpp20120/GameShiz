using System.Text.Json.Nodes;
using BotFramework.Sdk.Configuration;

namespace BotFramework.Host.Configuration.Validation;

internal interface IRegisteredConfigurationSection
{
    string SectionPath { get; }
    Type OptionsType { get; }
    ConfigurationSectionValidation Validate(JsonNode? patch);
    JsonNode Effective(JsonNode? patch);
}
