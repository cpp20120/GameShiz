using System.Collections.Frozen;
using System.Collections.Immutable;
using TextRules.Domain.Rules;

namespace TextRules.Application.Compilation;

/// <summary>
/// Immutable, concurrently reusable rule indexes for one effective scope and version.
/// </summary>
public sealed class CompiledRuleSnapshot
{
    public required long Version { get; init; }

    public required FrozenDictionary<string, ImmutableArray<CompiledTokenRule>> TokenRules { get; init; }

    public required FrozenDictionary<char, ImmutableArray<CompiledTokenRule>> PartialTokenRules { get; init; }

    public required FrozenDictionary<string, ImmutableArray<CompiledPhraseRule>> PhraseRules { get; init; }

    public required ImmutableArray<CompiledRegexRule> RegexRules { get; init; }

    public required ImmutableArray<TextRule> RuleDefinitions { get; init; }
}
