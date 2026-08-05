namespace TextRules.Application.Compilation;

public sealed record TextRuleCompilerOptions
{
    public TimeSpan RegexTimeout { get; init; } = TimeSpan.FromMilliseconds(250);
}
