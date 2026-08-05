namespace BotFramework.Text;

public sealed record AddReactionEffect(string Reaction, string? MessageId = null) : IMessageEffect
{
    public string Kind => "add_reaction";
}
