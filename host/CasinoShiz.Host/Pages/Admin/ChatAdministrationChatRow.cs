namespace CasinoShiz.Host.Pages.Admin;

public sealed record ChatAdministrationChatRow(
    long ChatId,
    string ChatType,
    string Title,
    bool IsEnabled,
    int MemberCount,
    int ActiveWarningCount,
    int OpenCaseCount,
    int PendingEffectCount,
    DateTime UpdatedAt);
