namespace TextRules.Domain.Rules;

public sealed record TextRuleOptions
{
    /// <summary>
    /// When false, a token rule may match a substring inside one normalized token.
    /// </summary>
    public bool MatchWholeToken { get; init; } = true;
}
