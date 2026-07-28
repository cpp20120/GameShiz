namespace BotFramework.Contracts.Tenancy;

internal static class OpaqueIdValidation
{
    public static string Validate(string? value, string name)
    {
        if (!TryValidate(value, out var reason))
            throw new ArgumentException(reason, name);

        return value!;
    }

    public static bool TryValidate(string? value, out string reason)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            reason = "An opaque id is required.";
            return false;
        }

        if (value.Length > 256)
        {
            reason = "An opaque id must contain at most 256 characters.";
            return false;
        }

        if (value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character) || character is '/' or '\\'))
        {
            reason = "An opaque id must not contain whitespace, path separators, or control characters.";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
