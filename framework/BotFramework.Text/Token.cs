namespace BotFramework.Text;

/// <summary>
/// A generic lexical token. Tokenization intentionally carries no business meaning.
/// </summary>
public sealed record Token(string Text, int Start, int Length)
{
    public TextSpan Span => new(Start, Length);
}
