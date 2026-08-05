using System.Collections.Immutable;

namespace TextRules.Application.Compilation;

public sealed record CompiledPhraseRule : CompiledRule
{
    public required ImmutableArray<string> Tokens { get; init; }
}
