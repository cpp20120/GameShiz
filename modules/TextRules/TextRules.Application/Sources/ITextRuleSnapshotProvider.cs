using TextRules.Application.Compilation;
using TextRules.Domain.Rules;

namespace TextRules.Application.Sources;

public interface ITextRuleSnapshotProvider
{
    ValueTask<CompiledRuleSnapshot> GetAsync(
        RuleScope scope,
        CancellationToken cancellationToken = default);

    ValueTask InvalidateAsync(
        RuleScope scope,
        CancellationToken cancellationToken = default);
}
