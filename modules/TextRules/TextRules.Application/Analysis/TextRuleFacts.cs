using BotFramework.Text;

namespace TextRules.Application.Analysis;

public static class TextRuleFacts
{
    public const string MatchesKey = "text_rules.matches";
    public const string DiagnosticsKey = "text_rules.diagnostics";

    public static IReadOnlyList<TextRuleMatchedFact> GetMatches(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Values.TryGetValue(MatchesKey, out var value)
            && value is IReadOnlyList<TextRuleMatchedFact> facts
            ? facts
            : [];
    }

    public static IEnumerable<TextRuleMatchedFact> GetEffectiveMatches(AnalysisResult result) =>
        GetMatches(result).Where(fact => fact.IsEffective);
}
