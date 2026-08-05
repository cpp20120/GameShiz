using BotFramework.Text;
using TextRules.Application.Compilation;
using TextRules.Domain.Matches;

namespace TextRules.Application.Matching;

public interface ITextRuleMatcher
{
    RuleMatchResult Match(
        NormalizedText text,
        CompiledRuleSnapshot snapshot);
}
