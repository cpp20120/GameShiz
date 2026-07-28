namespace BotFramework.Rendering;

public sealed record RenderOutput(byte[] Content, string? FileName = null)
{
    public static RenderOutput FromBytes(byte[] content, string? fileName = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new RenderOutput(content, fileName);
    }
}
