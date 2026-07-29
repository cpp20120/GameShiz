using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Services;

public sealed record ModerationAnalytics(
    ChatId ChatId,
    long Cases,
    long AppliedCases,
    long FailedCases,
    long UnknownCases,
    long ActiveWarnings,
    long IndexedMessages,
    IReadOnlyDictionary<string, long> CasesByAction);
