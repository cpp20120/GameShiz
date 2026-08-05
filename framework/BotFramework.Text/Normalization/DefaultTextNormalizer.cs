using System.Globalization;
using System.Text;

namespace BotFramework.Text;

/// <summary>
/// Safe, business-neutral Unicode normalization. Domain-specific transforms such as
/// homoglyph or leetspeak folding belong in consumer analyzers, not in the framework default.
/// </summary>
public sealed class DefaultTextNormalizer : ITextNormalizer
{
    private static readonly HashSet<int> ZeroWidthCharacters =
    [
        0x034F,
        0x180E,
        0x200B,
        0x200C,
        0x200D,
        0x2060,
        0x2061,
        0x2062,
        0x2063,
        0x2064,
        0xFEFF,
    ];

    private static readonly HashSet<int> BidirectionalControls =
    [
        0x061C,
        0x200E,
        0x200F,
        0x202A,
        0x202B,
        0x202C,
        0x202D,
        0x202E,
        0x2066,
        0x2067,
        0x2068,
        0x2069,
    ];

    private readonly TextNormalizerOptions _options;
    private readonly ITextTokenizer _tokenizer;

    public DefaultTextNormalizer(
        TextNormalizerOptions? options = null,
        ITextTokenizer? tokenizer = null)
    {
        _options = options ?? new TextNormalizerOptions();
        _tokenizer = tokenizer ?? new DefaultTextTokenizer();
    }

    public NormalizedText Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var signals = new HashSet<TextSignal>();
        var mappings = new List<TextSpanMapping>(text.Length);
        var canonical = new StringBuilder(text.Length);
        var scripts = new HashSet<string>(StringComparer.Ordinal);
        var pendingWhitespace = false;
        var pendingWhitespaceSource = default(TextSpan);
        var sourceStart = 0;

        while (sourceStart < text.Length)
        {
            var sourceLength = NextTextElementLength(text, sourceStart, signals);
            var sourceSpan = new TextSpan(sourceStart, sourceLength);
            var sourceElement = text.Substring(sourceStart, sourceLength);
            foreach (var sourceRune in sourceElement.EnumerateRunes())
                ObserveSourceRune(sourceRune, scripts, signals);

            var normalizedElement = NormalizeElement(sourceElement, signals);
            if (!string.Equals(sourceElement, normalizedElement, StringComparison.Ordinal))
                signals.Add(TextSignal.UnicodeNormalization);
            if (_options.LowerInvariant)
                normalizedElement = normalizedElement.ToLowerInvariant();

            foreach (var normalizedRune in normalizedElement.EnumerateRunes())
            {
                ObserveScript(normalizedRune, scripts);
                if (ShouldRemove(normalizedRune, signals))
                    continue;

                var rune = _options.NormalizeCommonPunctuation
                    ? NormalizePunctuation(normalizedRune, signals)
                    : normalizedRune;

                if (_options.CollapseWhitespace && Rune.IsWhiteSpace(rune))
                {
                    pendingWhitespace = true;
                    pendingWhitespaceSource = pendingWhitespaceSource == default
                        ? sourceSpan
                        : Merge(pendingWhitespaceSource, sourceSpan);
                    continue;
                }

                if (pendingWhitespace && (canonical.Length > 0 || !_options.TrimWhitespace))
                    AppendRune(canonical, mappings, new Rune(' '), pendingWhitespaceSource);

                pendingWhitespace = false;
                pendingWhitespaceSource = default;
                AppendRune(canonical, mappings, rune, sourceSpan);
            }

            sourceStart += sourceLength;
        }

        if (_options.CollapseWhitespace
            && pendingWhitespace
            && !_options.TrimWhitespace)
        {
            AppendRune(canonical, mappings, new Rune(' '), pendingWhitespaceSource);
        }

        if (scripts.Count > 1)
            signals.Add(TextSignal.MixedScripts);
        if (HasRepeatedRunes(canonical.ToString()))
            signals.Add(TextSignal.RepeatedCharacters);

        var canonicalText = canonical.ToString();
        return new NormalizedText
        {
            OriginalText = text,
            CanonicalText = canonicalText,
            Tokens = _tokenizer.Tokenize(canonicalText),
            Spans = mappings.ToArray(),
            Signals = signals,
        };
    }

    private int NextTextElementLength(
        string text,
        int sourceStart,
        HashSet<TextSignal> signals)
    {
        try
        {
            return StringInfo.GetNextTextElement(text, sourceStart).Length;
        }
        catch (ArgumentException)
        {
            signals.Add(TextSignal.InvalidUnicode);
            return 1;
        }
    }

    private string NormalizeElement(string sourceElement, HashSet<TextSignal> signals)
    {
        try
        {
            return sourceElement.Normalize(_options.UnicodeForm);
        }
        catch (ArgumentException)
        {
            signals.Add(TextSignal.InvalidUnicode);
            return sourceElement;
        }
    }

    private bool ShouldRemove(Rune rune, HashSet<TextSignal> signals)
    {
        var value = rune.Value;
        if (_options.RemoveZeroWidthCharacters && ZeroWidthCharacters.Contains(value))
        {
            signals.Add(TextSignal.ZeroWidth);
            signals.Add(TextSignal.SuspiciousFormatting);
            return true;
        }

        if (_options.RemoveBidirectionalControls && BidirectionalControls.Contains(value))
        {
            signals.Add(TextSignal.Bidirectional);
            signals.Add(TextSignal.SuspiciousFormatting);
            return true;
        }

        if (_options.RemoveFormatCharacters && Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format)
        {
            signals.Add(TextSignal.FormatCharacters);
            signals.Add(TextSignal.SuspiciousFormatting);
            return true;
        }

        return false;
    }

    private static void ObserveSourceRune(
        Rune rune,
        HashSet<string> scripts,
        HashSet<TextSignal> signals)
    {
        if (ZeroWidthCharacters.Contains(rune.Value))
        {
            signals.Add(TextSignal.ZeroWidth);
            signals.Add(TextSignal.SuspiciousFormatting);
        }

        if (BidirectionalControls.Contains(rune.Value))
        {
            signals.Add(TextSignal.Bidirectional);
            signals.Add(TextSignal.SuspiciousFormatting);
        }

        if (rune == Rune.ReplacementChar)
            signals.Add(TextSignal.InvalidUnicode);

        ObserveScript(rune, scripts);
    }

    private static void ObserveScript(Rune rune, HashSet<string> scripts)
    {
        if (!Rune.IsLetter(rune))
            return;

        var script = ScriptOf(rune.Value);
        if (script is not null)
            scripts.Add(script);
    }

    private static Rune NormalizePunctuation(Rune rune, HashSet<TextSignal> signals)
    {
        var replacement = rune.Value switch
        {
            0x2010 or 0x2011 or 0x2012 or 0x2013 or 0x2014 or 0x2015 or 0x2212 or 0xFE63 or 0xFF0D => '-',
            0x2018 or 0x2019 or 0x201B or 0x2032 or 0x00B4 or 0x0060 => '\'',
            0x201C or 0x201D or 0x201F or 0x2033 or 0xFF02 => '"',
            _ => 0,
        };

        if (replacement == 0)
            return rune;

        signals.Add(TextSignal.PunctuationNormalization);
        return new Rune(replacement);
    }

    private static void AppendRune(
        StringBuilder canonical,
        List<TextSpanMapping> mappings,
        Rune rune,
        TextSpan originalSpan)
    {
        Span<char> buffer = stackalloc char[2];
        var written = rune.EncodeToUtf16(buffer);
        var canonicalStart = canonical.Length;
        canonical.Append(buffer[..written]);
        mappings.Add(new TextSpanMapping(new TextSpan(canonicalStart, written), originalSpan));
    }

    private static TextSpan Merge(TextSpan first, TextSpan second)
    {
        var start = Math.Min(first.Start, second.Start);
        var end = Math.Max(first.End, second.End);
        return new TextSpan(start, end - start);
    }

    private static bool HasRepeatedRunes(string text)
    {
        Rune? previous = null;
        var repeated = 1;

        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                previous = null;
                repeated = 1;
                continue;
            }

            repeated = previous is { } value && value == rune ? repeated + 1 : 1;
            if (repeated >= 3)
                return true;

            previous = rune;
        }

        return false;
    }

    private static string? ScriptOf(int value) => value switch
    {
        >= 0x0041 and <= 0x024F => "latin",
        >= 0x0370 and <= 0x03FF => "greek",
        >= 0x0400 and <= 0x052F => "cyrillic",
        >= 0x0590 and <= 0x05FF => "hebrew",
        >= 0x0600 and <= 0x06FF => "arabic",
        >= 0x0900 and <= 0x097F => "devanagari",
        >= 0x3040 and <= 0x30FF => "japanese",
        >= 0x3400 and <= 0x9FFF => "han",
        >= 0x20000 and <= 0x2FA1F => "han",
        _ => null,
    };
}
