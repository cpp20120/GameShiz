using System.Buffers;
using System.Globalization;
using System.Text;

namespace BotFramework.Text;

/// <summary>
/// Unicode-aware tokenizer for words and numbers. Token spans use UTF-16 offsets.
/// </summary>
public sealed class DefaultTextTokenizer : ITextTokenizer
{
    public IReadOnlyList<Token> Tokenize(string canonicalText)
    {
        ArgumentNullException.ThrowIfNull(canonicalText);

        var tokens = new List<Token>();
        var tokenStart = -1;
        var index = 0;

        while (index < canonicalText.Length)
        {
            var status = Rune.DecodeFromUtf16(canonicalText.AsSpan(index), out var rune, out var consumed);
            if (status != OperationStatus.Done)
            {
                rune = Rune.ReplacementChar;
                consumed = 1;
            }

            if (IsTokenRune(rune))
            {
                tokenStart = tokenStart < 0 ? index : tokenStart;
            }
            else if (tokenStart >= 0)
            {
                tokens.Add(new Token(
                    canonicalText.Substring(tokenStart, index - tokenStart),
                    tokenStart,
                    index - tokenStart));
                tokenStart = -1;
            }

            index += consumed;
        }

        if (tokenStart >= 0)
        {
            tokens.Add(new Token(
                canonicalText.Substring(tokenStart),
                tokenStart,
                canonicalText.Length - tokenStart));
        }

        return tokens;
    }

    private static bool IsTokenRune(Rune rune) => Rune.GetUnicodeCategory(rune) is
        UnicodeCategory.UppercaseLetter
        or UnicodeCategory.LowercaseLetter
        or UnicodeCategory.TitlecaseLetter
        or UnicodeCategory.ModifierLetter
        or UnicodeCategory.OtherLetter
        or UnicodeCategory.NonSpacingMark
        or UnicodeCategory.SpacingCombiningMark
        or UnicodeCategory.DecimalDigitNumber
        or UnicodeCategory.LetterNumber
        or UnicodeCategory.OtherNumber
        or UnicodeCategory.ConnectorPunctuation;
}
