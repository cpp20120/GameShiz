using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed class ChallengeAdminRow
{
    public Guid Id { get; init; }
    public long ChatId { get; init; }
    public string ChatLabel { get; init; } = "";
    public long ChallengerId { get; init; }
    public string ChallengerName { get; init; } = "";
    public long TargetId { get; init; }
    public string TargetName { get; init; } = "";
    public int Amount { get; init; }
    public string Game { get; init; } = "";
    public string Status { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? RespondedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}
