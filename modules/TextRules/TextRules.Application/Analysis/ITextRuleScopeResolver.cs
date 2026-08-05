using BotFramework.Text;
using TextRules.Domain.Rules;

namespace TextRules.Application.Analysis;

public interface ITextRuleScopeResolver
{
    RuleScope Resolve(TextProcessingContext context);
}
