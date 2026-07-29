using System.Globalization;

namespace ChatAdministration.Application.Parsing;

public static class PurgeCommandParser
{
    public static bool TryParse(string text, out int count, out string? error)
    {
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 1 || tokens.Length == 2 && IsTarget(tokens[1]))
        {
            count = 1;
            error = null;
            return true;
        }

        if (tokens.Length != 2 || !int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count is < 1 or > 1000)
        {
            count = 0;
            error = "Использование: /purge [1..1000] ответом на сообщение.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsTarget(string token) =>
        token.StartsWith('@') || long.TryParse(token, out _);
}
