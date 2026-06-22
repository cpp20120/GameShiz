namespace BotFramework.Host;

public sealed record RenderedMessage(string Text, IReadOnlyList<InlineButton> Buttons);
