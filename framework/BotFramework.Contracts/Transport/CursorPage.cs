namespace BotFramework.Contracts.Transport;

/// <summary>Stable cursor response shared by list endpoints and generated clients.</summary>
public sealed record CursorPage<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    bool HasMore);
