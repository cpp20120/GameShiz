using System.Globalization;

namespace ChatAdministration.Application.Parsing;

public static class MuteCommandParser
{
    private static readonly Dictionary<char, double> Units = new()
    {
        ['s'] = 1,
        ['m'] = 60,
        ['h'] = 60 * 60,
        ['d'] = 60 * 60 * 24,
        ['w'] = 60 * 60 * 24 * 7,
    };

    public static bool TryParse(string text, out ParsedMute? result, out string? error)
    {
        result = null;
        error = null;
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var argumentStart = parts.Length > 1 && IsTarget(parts[1]) ? 2 : 1;
        if (parts.Length <= argumentStart)
        {
            error = "Использование: /mute 10m причина (ответом на сообщение пользователя).";
            return false;
        }

        var token = parts[argumentStart].ToLowerInvariant();
        if (token.Length < 2 || !Units.TryGetValue(token[^1], out var multiplier)
            || !double.TryParse(token[..^1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
            || amount <= 0 || double.IsNaN(amount) || double.IsInfinity(amount))
        {
            error = "Длительность должна быть вида 30s, 10m, 2h, 1d или 1w.";
            return false;
        }

        var seconds = amount * multiplier;
        if (seconds > TimeSpan.FromDays(365).TotalSeconds)
        {
            error = "Максимальная длительность мута — 365 дней.";
            return false;
        }

        var reason = parts.Length > argumentStart + 1 ? string.Join(' ', parts.Skip(argumentStart + 1)) : null;
        result = new ParsedMute(TimeSpan.FromSeconds(seconds), reason);
        return true;
    }

    private static bool IsTarget(string token) =>
        token.StartsWith('@') || long.TryParse(token, out _);
}
