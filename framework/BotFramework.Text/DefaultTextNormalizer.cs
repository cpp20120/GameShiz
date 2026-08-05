using System.Globalization;
using System.Text;

namespace BotFramework.Text;

public sealed class DefaultTextNormalizer(TextNormalizerOptions? options = null) : ITextNormalizer
{
    private static readonly HashSet<char> ZeroWidthCharacters =
    [
        '\u034F',
        '\u180E',
        '\u200B',
        '\u200C',
        '\u200D',
        '\u2060',
        '\u2061',
        '\u2062',
        '\u2063',
        '\u2064',
        '\uFEFF',
    ];

    private static readonly HashSet<char> BidirectionalControls =
    [
        '\u061C',
        '\u200E',
        '\u200F',
        '\u202A',
        '\u202B',
        '\u202C',
        '\u202D',
        '\u202E',
        '\u2066',
        '\u2067',
        '\u2068',
        '\u2069',
    ];

    private readonly TextNormalizerOptions _options = options ?? new TextNormalizerOptions();

    public NormalizedText Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var signals = new HashSet<TextSignal>();
        var mappings = new List<TextSpanMapping>();
        var canonical = new StringBuilder(text.Length);
        var pendingWhitespace = false;
        var pendingWhitespaceSource = default(TextSpan);
        var scriptKinds = new HashSet<string>(StringComparer.Ordinal);

        for (var sourceStart = 0; sourceStart < text.Length;)
        {
            var sourceLength = StringInfo.GetNextTextElement(text, sourceStart).Length;
            var sourceSpan = new TextSpan(sourceStart, sourceLength);
            var sourceElement = text.Substring(sourceStart, sourceLength);

            ObserveSourceElement(sourceElement, scriptKinds, signals);

            var normalizedElement = sourceElement.Normalize(_options.UnicodeForm);
            if (!string.Equals(normalizedElement, sourceElement, StringComparison.Ordinal))
                signals.Add(TextSignal.UnicodeNormalization);
            if (_options.LowerInvariant)
                normalizedElement = normalizedElement.ToLowerInvariant();
            if (_options.RemoveZeroWidthCharacters)
                normalizedElement = RemoveZeroWidth(normalizedElement, signals);

            foreach (var character in normalizedElement)
            {
                if (_options.CollapseWhitespace && char.IsWhiteSpace(character))
                {
                    pendingWhitespace = true;
                    pendingWhitespaceSource = pendingWhitespaceSource == default
                        ? sourceSpan
                        : Merge(pendingWhitespaceSource, sourceSpan);
                    continue;
                }

                if (pendingWhitespace && canonical.Length > 0 && _options.CollapseWhitespace)
                {
                    Append(canonical, mappings, ' ', pendingWhitespaceSource);
                }

                pendingWhitespace = false;
                pendingWhitespaceSource = default;
                Append(canonical, mappings, character, sourceSpan);
            }

            sourceStart += sourceLength;
        }

        if (_options.CollapseWhitespace && pendingWhitespace && !_options.TrimWhitespace && canonical.Length > 0)
        {
            Append(canonical, mappings, ' ', pendingWhitespaceSource);
        }

        if (scriptKinds.Count > 1)
            signals.Add(TextSignal.MixedScripts);
        if (HasRepeatedCharacters(canonical))
            signals.Add(TextSignal.RepeatedCharacters);

        var canonicalText = canonical.ToString();
        return new NormalizedText
        {
            OriginalText = text,
            CanonicalText = canonicalText,
            Tokens = Tokenize(canonicalText),
            Spans = mappings.ToArray(),
            Signals = signals,
        };
    }

    private static void ObserveSourceElement(
        string sourceElement,
        HashSet<string> scriptKinds,
        HashSet<TextSignal> signals)
    {
        foreach (var character in sourceElement)
        {
            if (ZeroWidthCharacters.Contains(character))
            {
                signals.Add(TextSignal.ZeroWidth);
                signals.Add(TextSignal.SuspiciousFormatting);
            }
            if (BidirectionalControls.Contains(character))
            {
                signals.Add(TextSignal.Bidirectional);
                signals.Add(TextSignal.SuspiciousFormatting);
            }

            if (char.IsLetter(character))
            {
                var script = ScriptOf(character);
                if (script is not null)
                    scriptKinds.Add(script);
            }
        }
    }

    private static string RemoveZeroWidth(string value, HashSet<TextSignal> signals)
    {
        if (!value.Any(ZeroWidthCharacters.Contains))
            return value;

        signals.Add(TextSignal.ZeroWidth);
        signals.Add(TextSignal.SuspiciousFormatting);
        return string.Concat(value.Where(character => !ZeroWidthCharacters.Contains(character)));
    }

    private static void Append(
        StringBuilder canonical,
        List<TextSpanMapping> mappings,
        char character,
        TextSpan originalSpan)
    {
        var canonicalStart = canonical.Length;
        canonical.Append(character);
        mappings.Add(new TextSpanMapping(new TextSpan(canonicalStart, 1), originalSpan));
    }

    private static TextSpan Merge(TextSpan first, TextSpan second)
    {
        var start = Math.Min(first.Start, second.Start);
        var end = Math.Max(first.End, second.End);
        return new TextSpan(start, end - start);
    }

    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var tokenStart = -1;

        for (var index = 0; index < text.Length; index++)
        {
            var tokenCharacter = char.IsLetterOrDigit(text[index])
                || char.GetUnicodeCategory(text[index]) is UnicodeCategory.NonSpacingMark
                    or UnicodeCategory.SpacingCombiningMark
                    or UnicodeCategory.ConnectorPunctuation;
            if (tokenCharacter)
            {
                tokenStart = tokenStart < 0 ? index : tokenStart;
                continue;
            }

            if (tokenStart >= 0)
            {
                tokens.Add(new Token(text.Substring(tokenStart, index - tokenStart), tokenStart, index - tokenStart));
                tokenStart = -1;
            }
        }

        if (tokenStart >= 0)
            tokens.Add(new Token(text.Substring(tokenStart), tokenStart, text.Length - tokenStart));

        return tokens;
    }

    private static bool HasRepeatedCharacters(StringBuilder text)
    {
        var repeated = 1;
        for (var index = 1; index < text.Length; index++)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                repeated = 1;
                continue;
            }

            repeated = text[index] == text[index - 1] ? repeated + 1 : 1;
            if (repeated >= 3)
                return true;
        }

        return false;
    }

    private static string? ScriptOf(char character) => character switch
    {
        >= '\u0041' and <= '\u024F' => "latin",
        >= '\u0370' and <= '\u03FF' => "greek",
        >= '\u0400' and <= '\u052F' => "cyrillic",
        >= '\u0590' and <= '\u05FF' => "hebrew",
        >= '\u0600' and <= '\u06FF' => "arabic",
        >= '\u0900' and <= '\u097F' => "devanagari",
        >= '\u3040' and <= '\u30FF' => "japanese",
        >= '\u3400' and <= '\u9FFF' => "han",
        _ => null,
    };
}
