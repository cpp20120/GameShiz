namespace BotFramework.Text;

public sealed record QueueEffect(string Queue, object? Payload = null) : IMessageEffect
{
    public string Kind => "queue";
}
