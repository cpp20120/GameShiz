namespace BotFramework.Host.Contracts.Rendering;

public sealed record RenderedMedia(
    RenderedMediaKind Kind,
    byte[] Content,
    string FileName,
    string? Caption,
    IReadOnlyList<InlineButton> Buttons);
