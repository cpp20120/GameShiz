using System.Text.RegularExpressions;
using BotFramework.Text;
using TextRules.Application.Compilation;
using TextRules.Domain.Matches;
using TextRules.Domain.Rules;

namespace TextRules.Application.Matching;

public sealed class TextRuleMatcher : ITextRuleMatcher
{
    public RuleMatchResult Match(
        NormalizedText text,
        CompiledRuleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(snapshot);

        var matches = new List<RuleMatch>();
        var diagnostics = new List<RuleMatchDiagnostic>();
        MatchTokens(text, snapshot, matches);
        MatchPhrases(text, snapshot, matches);
        MatchRegexes(text, snapshot, matches, diagnostics);
        return RuleMatchResolver.Resolve(matches, diagnostics);
    }

    private static void MatchTokens(
        NormalizedText text,
        CompiledRuleSnapshot snapshot,
        List<RuleMatch> matches)
    {
        foreach (var token in text.Tokens)
        {
            if (snapshot.TokenRules.TryGetValue(token.Text, out var exactRules))
            {
                foreach (var rule in exactRules)
                    matches.Add(CreateMatch(rule, token.Span, text, RuleMatchKind.Token));
            }

            if (token.Text.Length == 0
                || !snapshot.PartialTokenRules.TryGetValue(token.Text[0], out var partialRules))
            {
                continue;
            }

            foreach (var rule in partialRules)
            {
                var searchStart = 0;
                while (searchStart < token.Text.Length)
                {
                    var offset = token.Text.IndexOf(
                        rule.Pattern,
                        searchStart,
                        StringComparison.Ordinal);
                    if (offset < 0)
                        break;

                    matches.Add(CreateMatch(
                        rule,
                        new TextSpan(token.Start + offset, rule.Pattern.Length),
                        text,
                        RuleMatchKind.Token));
                    searchStart = offset + 1;
                }
            }
        }
    }

    private static void MatchPhrases(
        NormalizedText text,
        CompiledRuleSnapshot snapshot,
        List<RuleMatch> matches)
    {
        for (var tokenIndex = 0; tokenIndex < text.Tokens.Count; tokenIndex++)
        {
            var firstToken = text.Tokens[tokenIndex];
            if (!snapshot.PhraseRules.TryGetValue(firstToken.Text, out var candidates))
                continue;

            foreach (var rule in candidates)
            {
                if (tokenIndex + rule.Tokens.Length > text.Tokens.Count)
                    continue;

                var isMatch = true;
                for (var phraseIndex = 0; phraseIndex < rule.Tokens.Length; phraseIndex++)
                {
                    if (!string.Equals(
                            rule.Tokens[phraseIndex],
                            text.Tokens[tokenIndex + phraseIndex].Text,
                            StringComparison.Ordinal))
                    {
                        isMatch = false;
                        break;
                    }
                }

                if (!isMatch)
                    continue;

                var first = text.Tokens[tokenIndex].Span;
                var last = text.Tokens[tokenIndex + rule.Tokens.Length - 1].Span;
                matches.Add(CreateMatch(
                    rule,
                    new TextSpan(first.Start, last.End - first.Start),
                    text,
                    RuleMatchKind.Phrase));
            }
        }
    }

    private static void MatchRegexes(
        NormalizedText text,
        CompiledRuleSnapshot snapshot,
        List<RuleMatch> matches,
        List<RuleMatchDiagnostic> diagnostics)
    {
        foreach (var rule in snapshot.RegexRules)
        {
            try
            {
                foreach (System.Text.RegularExpressions.Match regexMatch in rule.Regex.Matches(text.CanonicalText))
                {
                    matches.Add(CreateMatch(
                        rule,
                        new TextSpan(regexMatch.Index, regexMatch.Length),
                        text,
                        RuleMatchKind.Regex));
                }
            }
            catch (RegexMatchTimeoutException exception)
            {
                diagnostics.Add(new RuleMatchDiagnostic(
                    rule.RuleId,
                    "regex_timeout",
                    $"Regex rule '{rule.RuleId}' exceeded its configured timeout: {exception.Message}"));
            }
        }
    }

    private static RuleMatch CreateMatch(
        CompiledRule rule,
        TextSpan canonicalSpan,
        NormalizedText text,
        RuleMatchKind matchKind)
    {
        var originalSpan = text.MapToOriginal(canonicalSpan)
            ?? throw new InvalidOperationException(
                $"Canonical span for rule '{rule.RuleId}' could not be mapped to the original text.");

        return new RuleMatch
        {
            RuleId = rule.RuleId,
            Disposition = rule.Disposition,
            Scope = rule.Scope,
            Priority = rule.Priority,
            PatternLength = rule.PatternLength,
            Category = rule.Category,
            Reason = rule.Reason,
            CanonicalSpan = canonicalSpan,
            OriginalSpan = originalSpan,
            MatchKind = matchKind,
            Confidence = 1d,
        };
    }
}
