namespace BotFramework.Host.Contracts.Rendering;

public sealed record RenderedMessage(string Text, IReadOnlyList<InlineButton> Buttons);
