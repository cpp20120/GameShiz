namespace ChatAdministration.Domain.Models;

public sealed record ModerationRuleDefinition
{
    public required RuleId Id { get; init; }
    public required ModerationRuleType Type { get; init; }
    public bool IsEnabled { get; init; } = true;
    public int Priority { get; init; }
    public int? ScoreOverride { get; init; }
}
