using System.Text.RegularExpressions;

namespace TextRules.Application.Compilation;

public sealed record CompiledRegexRule : CompiledRule
{
    public required Regex Regex { get; init; }
}
