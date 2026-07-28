using Microsoft.EntityFrameworkCore;

namespace Games.Challenges.Infrastructure.Persistence;

public sealed class ChallengeEntity
{
    public Guid Id { get; init; }
    public long ChatId { get; init; }
    public long ChallengerId { get; init; }
    public string ChallengerName { get; init; } = "";
    public long TargetId { get; init; }
    public string TargetName { get; init; } = "";
    public int Amount { get; init; }
    public string Game { get; init; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? RespondedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public static ChallengeEntity From(Challenge value) => new()
    {
        Id = value.Id, ChatId = value.ChatId, ChallengerId = value.ChallengerId,
        ChallengerName = value.ChallengerName, TargetId = value.TargetId, TargetName = value.TargetName,
        Amount = value.Amount, Game = value.Game.ToString(), Status = value.Status.ToString(),
        CreatedAt = value.CreatedAt, ExpiresAt = value.ExpiresAt,
    };

    public Challenge ToDomain() => new(
        Id, ChatId, ChallengerId, ChallengerName, TargetId, TargetName, Amount,
        Enum.Parse<ChallengeGame>(Game), Enum.Parse<ChallengeStatus>(Status), CreatedAt, ExpiresAt);
}
