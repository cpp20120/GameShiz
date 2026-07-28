namespace BotFramework.Rendering;

public sealed record RenderKey(
    string RendererId,
    string RendererVersion,
    string ContentHash,
    string Extension,
    string ContentType)
{
    public string Value => $"{RendererId}:{RendererVersion}:{ContentHash}";

    public string ObjectName =>
        $"artifacts/{Segment(RendererId)}/{Segment(RendererVersion)}/{Segment(ContentHash)}.{Segment(Extension).TrimStart('.') }";

    private static string Segment(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return string.Concat(value.Select(static ch =>
            char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '-'));
    }
}
