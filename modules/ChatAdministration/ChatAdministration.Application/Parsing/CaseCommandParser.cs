using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Parsing;

public static class CaseCommandParser
{
    public static bool TryParseId(string text, out ModerationCaseId caseId, out string error)
    {
        caseId = default;
        var parts = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out var value))
        {
            error = "Используйте: /case <case-id> или /revoke <case-id>.";
            return false;
        }

        caseId = new ModerationCaseId(value);
        error = string.Empty;
        return true;
    }
}
