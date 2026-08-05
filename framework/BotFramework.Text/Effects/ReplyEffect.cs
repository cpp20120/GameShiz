namespace BotFramework.Text;

public sealed record ReplyEffect(
    string Text,
    string? ReplyToMessageId = null) : IMessageEffect
{
    public string Kind => "reply";
}
