namespace BotFramework.Host;

public sealed record RenderedMedia(
    RenderedMediaKind Kind,
    byte[] Content,
    string FileName,
    string? Caption,
    IReadOnlyList<InlineButton> Buttons);
