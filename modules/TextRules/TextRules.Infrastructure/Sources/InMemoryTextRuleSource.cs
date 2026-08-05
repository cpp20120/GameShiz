using System.Collections.Concurrent;
using System.Collections.Immutable;
using TextRules.Application.Sources;
using TextRules.Domain.Rules;

namespace TextRules.Infrastructure.Sources;

/// <summary>
/// Thread-safe rule source for tests and hosts that supply rules programmatically.
/// </summary>
public sealed class InMemoryTextRuleSource : ITextRuleSource
{
    private readonly ConcurrentDictionary<RuleScope, RuleSet> _ruleSets = new();

    public void Replace(RuleScope scope, RuleSet ruleSet)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(ruleSet);
        TextRuleValidator.EnsureValid(ruleSet);

        _ruleSets[scope] = new RuleSet
        {
            Version = ruleSet.Version,
            Rules = ruleSet.Rules.ToImmutableArray(),
        };
    }

    public void Replace(
        RuleScope scope,
        long version,
        IEnumerable<TextRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Replace(scope, new RuleSet
        {
            Version = version,
            Rules = rules.ToImmutableArray(),
        });
    }

    public bool Remove(RuleScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return _ruleSets.TryRemove(scope, out _);
    }

    public ValueTask<RuleSet> LoadAsync(
        RuleScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        var applicable = GetApplicableRuleSets(scope);
        var rules = applicable
            .SelectMany(ruleSet => ruleSet.Rules)
            .ToImmutableArray();
        var version = applicable.Count == 0 ? 1 : applicable.Max(ruleSet => ruleSet.Version);
        return ValueTask.FromResult<RuleSet>(new RuleSet
        {
            Version = version,
            Rules = rules,
        });
    }

    private List<RuleSet> GetApplicableRuleSets(RuleScope scope)
    {
        var result = new List<RuleSet>(3);
        if (_ruleSets.TryGetValue(RuleScope.Global, out var global))
            result.Add(global);

        if (scope.TenantId is not null
            && _ruleSets.TryGetValue(RuleScope.ForTenant(scope.TenantId), out var tenant))
        {
            result.Add(tenant);
        }

        if (scope.ChatId is not null
            && _ruleSets.TryGetValue(RuleScope.ForChat(scope.TenantId!, scope.ChatId), out var chat))
        {
            result.Add(chat);
        }

        return result;
    }
}
