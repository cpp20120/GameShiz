namespace BotFramework.Host.Contracts.Telegram;

/// <summary>
/// Transport-neutral text formatting requested for an outbound client message.
/// Telegram adapters map it to the platform-specific parse mode.
/// </summary>
public enum OutboundParseMode
{
    None,
    Html,
    Markdown,
    MarkdownV2,
}
