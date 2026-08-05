namespace BotFramework.Text;

public sealed record IgnoreEffect : IMessageEffect
{
    public string Kind => "ignore";
}
