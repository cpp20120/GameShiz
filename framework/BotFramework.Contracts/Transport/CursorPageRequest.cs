namespace BotFramework.Contracts.Transport;

/// <summary>Stable cursor request shared by list endpoints and generated clients.</summary>
public readonly record struct CursorPageRequest(string? Cursor = null, int Limit = 50)
{
    public CursorPageRequest Normalize()
    {
        if (Limit is < 1 or > 100)
            throw new InvalidOperationException("Limit must be between 1 and 100.");

        return this with { Cursor = string.IsNullOrWhiteSpace(Cursor) ? null : Cursor.Trim() };
    }
}
