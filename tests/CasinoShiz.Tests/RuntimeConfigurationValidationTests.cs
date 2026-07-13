using System.Text.Json.Nodes;
using BotFramework.Host.Configuration.RuntimeTuning;
using BotFramework.Host.Configuration.Validation;
using Games.Horse.Domain.Configuration;
using Games.Horse.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class RuntimeConfigurationValidationTests
{
    [Fact]
    public void UnknownNestedProperty_IsRejectedInsteadOfSilentlyIgnored()
    {
        var validator = CreateHorseValidator();

        var result = validator.Validate("""
            { "Games": { "horse": { "HorseCounnt": 8 } } }
            """);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Path == HorseOptions.SectionName && issue.Code == "invalid_json_shape");
    }

    [Fact]
    public void SemanticFailure_HasTypedSectionPath()
    {
        var validator = CreateHorseValidator();

        var result = validator.Validate("""
            { "Games": { "horse": { "HorseCount": 1 } } }
            """);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Path == "Games:horse:HorseCount");
    }

    [Fact]
    public void ValidPatch_ReturnsNormalizedPatchAndEffectivePreview()
    {
        var validator = CreateHorseValidator();

        var result = validator.Validate("""
            { "Games": { "horse": { "HorseCount": 6 } } }
            """);

        Assert.True(result.IsValid);
        Assert.Equal(6, JsonNode.Parse(result.NormalizedPatchJson)!["Games"]!["horse"]!["HorseCount"]!.GetValue<int>());
        Assert.Equal(6, JsonNode.Parse(result.EffectiveJson)!["Games"]!["horse"]!["HorseCount"]!.GetValue<int>());
        Assert.Equal(4, JsonNode.Parse(result.EffectiveJson)!["Games"]!["horse"]!["MinBetsToRun"]!.GetValue<int>());
    }

    [Fact]
    public void UnregisteredAdminSection_IsRejected()
    {
        var validator = CreateHorseValidator();

        var result = validator.Validate("""
            { "Games": { "not-a-game": { "Enabled": true } } }
            """);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "unknown_section");
    }

    [Fact]
    public void UnknownEmptyRoot_IsNotSilentlySanitizedAway()
    {
        var validator = CreateHorseValidator();

        var result = validator.Validate("{ \"Typo\": {} }");

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "unknown_root_section");
    }

    private static RuntimeConfigurationValidator CreateHorseValidator()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Games:horse:HorseCount"] = "4",
                ["Games:horse:MinBetsToRun"] = "4",
            })
            .Build();
        var section = new RegisteredConfigurationSection<HorseOptions>(
            HorseOptions.SectionName,
            configuration,
            [new HorseOptionsValidator()]);
        return new RuntimeConfigurationValidator([section]);
    }
}
