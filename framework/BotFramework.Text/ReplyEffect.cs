namespace BotFramework.Text;

public sealed record ReplyEffect(string Text) : IMessageEffect
{
    public string Kind => "reply";
}
