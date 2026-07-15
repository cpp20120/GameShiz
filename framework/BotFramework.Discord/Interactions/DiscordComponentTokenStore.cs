using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace BotFramework.Discord.Interactions;

/// <summary>
/// Issues component ids bound to this bot process. The generation is intentionally
/// rotated on every construction, so messages left over from a restart cannot
/// invoke an old game action.
/// </summary>
public sealed class DiscordComponentTokenStore : IDiscordComponentTokenStore
{
    private readonly string _generation = Convert.ToHexString(RandomNumberGenerator.GetBytes(5)).ToLowerInvariant();
    private readonly ConcurrentDictionary<string, TokenEntry> _tokens = new(StringComparer.Ordinal);

    public string Issue(string action, string? payload = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        var id = $"cs:{_generation}:{Convert.ToHexString(RandomNumberGenerator.GetBytes(10)).ToLowerInvariant()}";
        _tokens[id] = new TokenEntry(new DiscordComponentToken(action, payload ?? string.Empty), DateTimeOffset.UtcNow);
        TrimIfNeeded();
        return id;
    }

    public bool TryResolve(string customId, out DiscordComponentToken token)
    {
        token = new DiscordComponentToken(string.Empty, string.Empty);
        if (!customId.StartsWith($"cs:{_generation}:", StringComparison.Ordinal)
            || !_tokens.TryGetValue(customId, out var entry))
            return false;

        token = entry.Token;
        return true;
    }

    private void TrimIfNeeded()
    {
        if (_tokens.Count <= 10_000) return;
        foreach (var key in _tokens.OrderBy(x => x.Value.CreatedAt).Take(_tokens.Count - 10_000).Select(x => x.Key))
            _tokens.TryRemove(key, out _);
    }

    private sealed record TokenEntry(DiscordComponentToken Token, DateTimeOffset CreatedAt);
}
