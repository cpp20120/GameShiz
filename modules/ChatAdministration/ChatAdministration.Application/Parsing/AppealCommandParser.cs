using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Parsing;

public static class AppealCommandParser
{
    public static bool TryParseOpen(string text, out ModerationCaseId caseId, out string appealText, out string error)
    {
        caseId = default;
        appealText = string.Empty;
        var parts = text.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || !Guid.TryParse(parts[1], out var value) || string.IsNullOrWhiteSpace(parts[2]))
        {
            error = "Используйте: /appeal <case-id> <текст>.";
            return false;
        }

        caseId = new ModerationCaseId(value);
        appealText = parts[2];
        error = string.Empty;
        return true;
    }

    public static bool TryParseResolution(string text, out AppealId appealId, out string? comment, out string error)
    {
        appealId = default;
        comment = null;
        var parts = text.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !Guid.TryParse(parts[1], out var value))
        {
            error = "Используйте: /approveappeal <appeal-id> [комментарий].";
            return false;
        }

        appealId = new AppealId(value);
        comment = parts.Length == 3 ? parts[2] : null;
        error = string.Empty;
        return true;
    }
}
