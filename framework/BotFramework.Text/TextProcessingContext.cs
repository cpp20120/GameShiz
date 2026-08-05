namespace BotFramework.Text;

public sealed record TextProcessingContext
{
    public string? MessageId { get; init; }
    public string? Source { get; init; }
    public IReadOnlyDictionary<string, object?> Properties { get; init; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);
}
