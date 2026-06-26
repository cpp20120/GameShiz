// ─────────────────────────────────────────────────────────────────────────────
// Aggregate + persistence abstractions.
//
// Application services depend on IRepository<T>. The concrete repo might be
// EF-backed (classical) or event-store-backed (event-sourced) — services don't
// care. That's the whole point: persistence choice is a module decision, not a
// service decision.
// ─────────────────────────────────────────────────────────────────────────────

using System.Globalization;

namespace BotFramework.Sdk.Domain;

public sealed class ConcurrencyException : Exception
{
    public ConcurrencyException()
    {
    }

    public ConcurrencyException(string? message) : base(message)
    {
    }

    public ConcurrencyException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    public ConcurrencyException(string streamId, long expected, long actual)
        : base(string.Create(CultureInfo.InvariantCulture, $"Stream {streamId} expected version {expected}, was {actual}"))
    {
    }
}
