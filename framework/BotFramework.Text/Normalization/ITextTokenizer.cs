namespace BotFramework.Text;

/// <summary>
/// Splits canonical text into business-neutral lexical tokens.
/// </summary>
public interface ITextTokenizer
{
    IReadOnlyList<Token> Tokenize(string canonicalText);
}
