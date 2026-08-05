namespace BotFramework.Text;

/// <summary>
/// Replaces the current bot-owned reaction set for a message. An empty collection clears reactions.
/// </summary>
public sealed record SetMessageReactionsEffect(
    IReadOnlyList<string> Reactions,
    string? MessageId = null,
    bool IsBig = false) : IMessageEffect
{
    public string Kind => "set_message_reactions";
}
