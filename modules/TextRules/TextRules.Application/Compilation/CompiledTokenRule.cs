namespace TextRules.Application.Compilation;

public sealed record CompiledTokenRule : CompiledRule
{
    public required bool MatchWholeToken { get; init; }
}
