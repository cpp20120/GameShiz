namespace BotFramework.Text;

public sealed record DeleteMessageEffect(string? MessageId = null) : IMessageEffect
{
    public string Kind => "delete_message";
}
