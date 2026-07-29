using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;

namespace ChatAdministration.Telegram.Infrastructure;

public sealed class DefaultModerationRuleProvider : IModerationRuleProvider
{
    public IReadOnlyCollection<IModerationRule> GetRules(ChatState chat)
    {
        var defaults = new (ModerationRuleDefinition Definition, IModerationRule Rule)[]
        {
            (new ModerationRuleDefinition { Id = new RuleId("flood"), Type = ModerationRuleType.Flood }, new FloodRule(new RuleId("flood"), chat.Settings.FloodPolicy)),
            (new ModerationRuleDefinition { Id = new RuleId("duplicate_message"), Type = ModerationRuleType.DuplicateMessage }, new DuplicateMessageRule(new RuleId("duplicate_message"))),
            (new ModerationRuleDefinition { Id = new RuleId("links"), Type = ModerationRuleType.Link }, new LinkRule(new RuleId("links"), chat.Settings.LinkPolicy)),
            (new ModerationRuleDefinition { Id = new RuleId("caps"), Type = ModerationRuleType.Caps }, new CapsRule(new RuleId("caps"))),
            (new ModerationRuleDefinition { Id = new RuleId("forbidden_words"), Type = ModerationRuleType.ForbiddenWords }, new ForbiddenWordsRule(new RuleId("forbidden_words"), chat.Settings.ForbiddenWordsPolicy)),
            (new ModerationRuleDefinition { Id = new RuleId("mention_spam"), Type = ModerationRuleType.MentionSpam }, new MentionSpamRule(new RuleId("mention_spam"), chat.Settings.MentionSpamPolicy)),
            (new ModerationRuleDefinition { Id = new RuleId("forwarded_message"), Type = ModerationRuleType.ForwardedMessage }, new ForwardedMessageRule(new RuleId("forwarded_message"), chat.Settings.ForwardedMessagePolicy)),
            (new ModerationRuleDefinition { Id = new RuleId("media_type"), Type = ModerationRuleType.MediaType }, new MediaTypeRule(new RuleId("media_type"), chat.Settings.MediaTypePolicy)),
            (new ModerationRuleDefinition { Id = new RuleId("new_member"), Type = ModerationRuleType.NewMember }, new NewMemberRule(new RuleId("new_member"), chat.Settings.NewMemberPolicy)),
            (new ModerationRuleDefinition { Id = new RuleId("command_spam"), Type = ModerationRuleType.CommandSpam }, new CommandSpamRule(new RuleId("command_spam"), chat.Settings.CommandSpamPolicy)),
        };

        var configured = chat.Settings.ModerationRules.ToDictionary(rule => rule.Id.Value, StringComparer.OrdinalIgnoreCase);
        return defaults
            .Select(item =>
            {
                if (!configured.TryGetValue(item.Definition.Id.Value, out var setting))
                    return (item.Definition, Rule: (IModerationRule?)item.Rule);
                if (!setting.IsEnabled)
                    return (setting, Rule: (IModerationRule?)null);
                var rule = setting.ScoreOverride is { } score
                    ? new ScoreOverrideModerationRule(item.Rule, score)
                    : item.Rule;
                return (setting, Rule: (IModerationRule?)rule);
            })
            .Where(item => item.Rule is not null)
            .OrderBy(item => item.Item1.Priority)
            .Select(item => item.Rule!)
            .ToArray();
    }
}
