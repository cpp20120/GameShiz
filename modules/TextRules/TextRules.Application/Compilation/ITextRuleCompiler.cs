using TextRules.Domain.Rules;

namespace TextRules.Application.Compilation;

public interface ITextRuleCompiler
{
    CompiledRuleSnapshot Compile(RuleSet ruleSet);
}
