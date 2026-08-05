using TextRules.Domain.Rules;

namespace TextRules.Application.Sources;

public interface ITextRuleSource
{
    ValueTask<RuleSet> LoadAsync(
        RuleScope scope,
        CancellationToken cancellationToken = default);
}
