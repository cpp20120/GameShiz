namespace BotFramework.Text;

/// <summary>
/// Immutable publication boundary for consumer-owned compiled indexes.
/// </summary>
public sealed record CompiledSnapshot<T>
{
    public required T Value { get; init; }
    public required string Version { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
