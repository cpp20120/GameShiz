using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed class CommandSpamRule(RuleId id, CommandSpamPolicy policy) : IModerationRule
{
    public RuleId Id { get; } = id;

    public Violation? Evaluate(ModerationMessageContext context)
    {
        var isCommand = context.Message.Entities.Any(entity => entity.Type == MessageEntityType.BotCommand);
        if (!policy.Enabled || !isCommand || policy.MaximumCommands <= 0 || context.RateLimits.CommandsInWindow <= policy.MaximumCommands)
            return null;

        return new Violation
        {
            RuleId = Id,
            Code = "command_spam",
            Score = Math.Max(1, policy.Score),
            Severity = ViolationSeverity.High,
            Reason = "Слишком много команд за короткое время.",
            Metadata = new Dictionary<string, object?>
            {
                ["commands"] = context.RateLimits.CommandsInWindow,
                ["window_seconds"] = policy.Window.TotalSeconds,
            },
        };
    }
}
