using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using BotFramework.Text;
using TextRules.Domain.Rules;

namespace TextRules.Application.Compilation;

public sealed class TextRuleCompiler : ITextRuleCompiler
{
    private readonly ITextNormalizer _normalizer;
    private readonly TextRuleCompilerOptions _options;

    public TextRuleCompiler(
        ITextNormalizer normalizer,
        TextRuleCompilerOptions? options = null)
    {
        _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
        _options = options ?? new TextRuleCompilerOptions();
        if (_options.RegexTimeout <= TimeSpan.Zero || _options.RegexTimeout == Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Regex timeout must be positive and finite.");
        }
    }

    public CompiledRuleSnapshot Compile(RuleSet ruleSet)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        TextRuleValidator.EnsureValid(ruleSet);

        var orderedRules = ruleSet.Rules
            .OrderBy(rule => rule.Id.Value, StringComparer.Ordinal)
            .ThenBy(rule => rule.Kind)
            .ThenBy(rule => rule.Pattern, StringComparer.Ordinal)
            .ToArray();
        var prepared = PrepareRules(orderedRules);
        var indexes = BuildIndexes(prepared);

        return new CompiledRuleSnapshot
        {
            Version = ruleSet.Version,
            TokenRules = Freeze(indexes.TokenRules),
            PartialTokenRules = Freeze(indexes.PartialTokenRules),
            PhraseRules = Freeze(indexes.PhraseRules),
            RegexRules = indexes.RegexRules
                .OrderBy(rule => rule.RuleId.Value, StringComparer.Ordinal)
                .ThenByDescending(rule => rule.Priority)
                .ToImmutableArray(),
            RuleDefinitions = orderedRules.ToImmutableArray(),
        };
    }

    private List<PreparedRule> PrepareRules(TextRule[] orderedRules)
    {
        var patternErrors = new List<TextRuleValidationError>();
        var prepared = new List<PreparedRule>(orderedRules.Length);

        foreach (var rule in orderedRules)
        {
            var normalized = _normalizer.Normalize(rule.Pattern);
            var canonicalPattern = normalized.CanonicalText;
            if (canonicalPattern.Length == 0)
            {
                patternErrors.Add(new TextRuleValidationError(
                    rule.Id,
                    "empty_normalized_pattern",
                    $"Rule '{rule.Id}' becomes empty after normalization."));
                continue;
            }

            if (rule.Kind == TextRuleKind.Token && normalized.Tokens.Count != 1)
            {
                patternErrors.Add(new TextRuleValidationError(
                    rule.Id,
                    "token_pattern",
                    $"Token rule '{rule.Id}' must normalize to exactly one token."));
                continue;
            }
            if (rule.Kind == TextRuleKind.Phrase && normalized.Tokens.Count == 0)
            {
                patternErrors.Add(new TextRuleValidationError(
                    rule.Id,
                    "phrase_pattern",
                    $"Phrase rule '{rule.Id}' must normalize to at least one token."));
                continue;
            }

            Regex? regex = null;
            if (rule.Kind == TextRuleKind.Regex)
            {
                try
                {
                    regex = CompileRegex(canonicalPattern);
                }
                catch (ArgumentException exception)
                {
                    patternErrors.Add(new TextRuleValidationError(
                        rule.Id,
                        "regex_pattern",
                        $"Rule '{rule.Id}' has an invalid regex: {exception.Message}"));
                    continue;
                }
            }

            prepared.Add(new PreparedRule(
                rule,
                canonicalPattern,
                normalized.Tokens.Select(token => token.Text).ToImmutableArray(),
                regex));
        }

        if (patternErrors.Count > 0)
            throw new TextRuleValidationException(patternErrors);

        return prepared;
    }

    private static CompiledIndexes BuildIndexes(IEnumerable<PreparedRule> prepared)
    {
        var tokenRules = new Dictionary<string, List<CompiledTokenRule>>(StringComparer.Ordinal);
        var partialTokenRules = new Dictionary<char, List<CompiledTokenRule>>();
        var phraseRules = new Dictionary<string, List<CompiledPhraseRule>>(StringComparer.Ordinal);
        var regexRules = new List<CompiledRegexRule>();

        foreach (var item in prepared.Where(item => item.Rule.Enabled))
        {
            switch (item.Rule.Kind)
            {
                case TextRuleKind.Token:
                {
                    var compiled = CreateTokenRule(item);
                    if (compiled.MatchWholeToken)
                        Add(tokenRules, compiled.Pattern, compiled);
                    else
                        Add(partialTokenRules, compiled.Pattern[0], compiled);
                    break;
                }
                case TextRuleKind.Phrase:
                {
                    var compiled = CreatePhraseRule(item);
                    Add(phraseRules, compiled.Tokens[0], compiled);
                    break;
                }
                case TextRuleKind.Regex:
                    regexRules.Add(CreateRegexRule(item));
                    break;
            }
        }

        return new CompiledIndexes(tokenRules, partialTokenRules, phraseRules, regexRules);
    }

    private Regex CompileRegex(string pattern)
    {
        var preferredOptions = RegexOptions.Compiled
            | RegexOptions.CultureInvariant
            | RegexOptions.NonBacktracking;
        try
        {
            return new Regex(pattern, preferredOptions, _options.RegexTimeout);
        }
        catch (ArgumentException)
        {
            return new Regex(
                pattern,
                RegexOptions.Compiled | RegexOptions.CultureInvariant,
                _options.RegexTimeout);
        }
    }

    private static CompiledTokenRule CreateTokenRule(PreparedRule item) => new()
    {
        RuleId = item.Rule.Id,
        Disposition = item.Rule.Disposition,
        Scope = item.Rule.Scope,
        Priority = item.Rule.Priority,
        Category = item.Rule.Category,
        Reason = item.Rule.Reason,
        Pattern = item.CanonicalPattern,
        PatternLength = item.CanonicalPattern.Length,
        MatchWholeToken = item.Rule.Options.MatchWholeToken,
    };

    private static CompiledPhraseRule CreatePhraseRule(PreparedRule item) => new()
    {
        RuleId = item.Rule.Id,
        Disposition = item.Rule.Disposition,
        Scope = item.Rule.Scope,
        Priority = item.Rule.Priority,
        Category = item.Rule.Category,
        Reason = item.Rule.Reason,
        Pattern = item.CanonicalPattern,
        PatternLength = item.CanonicalPattern.Length,
        Tokens = item.Tokens,
    };

    private static CompiledRegexRule CreateRegexRule(PreparedRule item) => new()
    {
        RuleId = item.Rule.Id,
        Disposition = item.Rule.Disposition,
        Scope = item.Rule.Scope,
        Priority = item.Rule.Priority,
        Category = item.Rule.Category,
        Reason = item.Rule.Reason,
        Pattern = item.CanonicalPattern,
        PatternLength = item.CanonicalPattern.Length,
        Regex = item.Regex!,
    };

    private static FrozenDictionary<TKey, ImmutableArray<TValue>> Freeze<TKey, TValue>(
        Dictionary<TKey, List<TValue>> source)
        where TKey : notnull => source.ToFrozenDictionary(
            pair => pair.Key,
            pair => pair.Value.ToImmutableArray());

    private static void Add<TKey, TValue>(Dictionary<TKey, List<TValue>> index, TKey key, TValue value)
        where TKey : notnull
    {
        if (!index.TryGetValue(key, out var values))
        {
            values = [];
            index[key] = values;
        }

        values.Add(value);
    }

    private sealed record PreparedRule(
        TextRule Rule,
        string CanonicalPattern,
        ImmutableArray<string> Tokens,
        Regex? Regex);

    private sealed record CompiledIndexes(
        Dictionary<string, List<CompiledTokenRule>> TokenRules,
        Dictionary<char, List<CompiledTokenRule>> PartialTokenRules,
        Dictionary<string, List<CompiledPhraseRule>> PhraseRules,
        List<CompiledRegexRule> RegexRules);
}
