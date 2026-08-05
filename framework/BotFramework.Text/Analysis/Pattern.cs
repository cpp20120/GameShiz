namespace BotFramework.Text;

/// <summary>
/// A minimal, business-neutral pattern description.
/// </summary>
public sealed record Pattern
{
    public required string Id { get; init; }
    public required string Kind { get; init; }
    public required string Value { get; init; }
    public IReadOnlyDictionary<string, string> Options { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
