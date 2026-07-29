using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record Violation
{
    public required RuleId RuleId { get; init; }
    public required string Code { get; init; }
    public required int Score { get; init; }
    public required ViolationSeverity Severity { get; init; }
    public required string Reason { get; init; }
    public IReadOnlyDictionary<string, object?> Metadata { get; init; } = new Dictionary<string, object?>();
}
