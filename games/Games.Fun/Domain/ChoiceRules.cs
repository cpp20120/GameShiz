namespace Games.Fun.Domain;

public static class ChoiceRules
{
    public const int MinOptions = 2;
    public const int MaxOptions = 50;
    public const int MaxOptionLength = 50;

    public static bool TryParse(
        string? raw,
        out IReadOnlyList<string> options,
        out ChoiceError? error)
    {
        options = [];
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = ChoiceError.Empty;
            return false;
        }

        var normalized = raw.Trim()
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        normalized = normalized
            .Replace(",\n", ",", StringComparison.Ordinal)
            .Replace("\n,", ",", StringComparison.Ordinal);
        var parts = normalized.Split([',', '\n'], StringSplitOptions.None);
        var parsed = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            var option = part.Trim();
            if (option.Length == 0)
            {
                error = ChoiceError.EmptyOption;
                return false;
            }

            if (option.Length > MaxOptionLength)
            {
                error = ChoiceError.OptionTooLong;
                return false;
            }

            parsed.Add(option);
        }

        if (parsed.Count < MinOptions)
        {
            error = ChoiceError.TooFew;
            return false;
        }

        if (parsed.Count > MaxOptions)
        {
            error = ChoiceError.TooMany;
            return false;
        }

        options = parsed;
        return true;
    }
}
