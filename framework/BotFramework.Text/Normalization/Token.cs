namespace BotFramework.Text;

/// <summary>
/// A generic lexical token. Tokenization intentionally carries no business meaning.
/// </summary>
public sealed record Token
{
    public Token(string text, int start, int length)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length != length)
            throw new ArgumentException("Token text length must equal the declared UTF-16 length.", nameof(length));

        Text = text;
        Span = new TextSpan(start, length);
    }

    public string Text { get; }
    public TextSpan Span { get; }
    public int Start => Span.Start;
    public int Length => Span.Length;
}
