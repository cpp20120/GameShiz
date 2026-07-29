namespace ChatAdministration.Domain.Models;

public sealed record CaptchaPolicy
{
    public bool Enabled { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);
    public int MaximumAttempts { get; init; } = 3;
    public CaptchaFailureAction FailureAction { get; init; } = CaptchaFailureAction.Kick;
    public bool DeleteChallengeAfterCompletion { get; init; } = true;
}
