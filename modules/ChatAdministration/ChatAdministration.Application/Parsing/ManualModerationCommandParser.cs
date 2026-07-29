using System.Globalization;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Parsing;

public static class ManualModerationCommandParser
{
    private static readonly Dictionary<char, double> Units = new()
    {
        ['s'] = 1,
        ['m'] = 60,
        ['h'] = 60 * 60,
        ['d'] = 60 * 60 * 24,
        ['w'] = 60 * 60 * 24 * 7,
    };

    public static bool TryParse(
        string text,
        ModerationAction action,
        out ParsedManualModeration? result,
        out string? error)
    {
        result = null;
        error = null;
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var arguments = parts.Skip(1).ToArray();
        if (arguments.Length > 0 && IsTarget(arguments[0]))
            arguments = arguments.Skip(1).ToArray();
        TimeSpan? duration = null;
        var reasonStart = 0;

        if (action == ModerationAction.Ban && arguments.Length > 0 && TryParseDuration(arguments[0], out var parsedDuration))
        {
            duration = parsedDuration;
            reasonStart = 1;
        }
        else if (action == ModerationAction.Ban && arguments.Length > 0 && LooksLikeDuration(arguments[0]))
        {
            error = "Длительность должна быть вида 30s, 10m, 2h, 1d или 1w.";
            return false;
        }

        var reason = reasonStart < arguments.Length ? string.Join(' ', arguments[reasonStart..]) : null;
        if (action == ModerationAction.Ban && duration is null && arguments.Length > 0 && LooksLikeDuration(arguments[0]))
        {
            error = "Длительность должна быть вида 30s, 10m, 2h, 1d или 1w.";
            return false;
        }

        result = new ParsedManualModeration(duration, string.IsNullOrWhiteSpace(reason) ? null : reason);
        return true;
    }

    private static bool TryParseDuration(string token, out TimeSpan duration)
    {
        duration = default;
        token = token.ToLowerInvariant();
        if (token.Length < 2 || !Units.TryGetValue(token[^1], out var multiplier)
            || !double.TryParse(token[..^1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
            || amount <= 0 || double.IsNaN(amount) || double.IsInfinity(amount))
            return false;

        var seconds = amount * multiplier;
        if (seconds > TimeSpan.FromDays(365).TotalSeconds)
            return false;

        duration = TimeSpan.FromSeconds(seconds);
        return true;
    }

    private static bool LooksLikeDuration(string token) =>
        token.Length > 0 && (char.IsDigit(token[0]) || token[0] == '.') && char.IsLetter(token[^1]);

    private static bool IsTarget(string token) =>
        token.StartsWith('@') || long.TryParse(token, out _);
}
