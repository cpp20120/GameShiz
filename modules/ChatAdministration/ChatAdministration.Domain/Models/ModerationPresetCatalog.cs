namespace ChatAdministration.Domain.Models;

public static class ModerationPresetCatalog
{
    public const string Default = "default";
    public const string Relaxed = "relaxed";
    public const string Strict = "strict";
    public const string Disabled = "disabled";

    public static bool TryApply(string? preset, ChatSettings current, out ChatSettings updated)
    {
        var template = preset?.Trim().ToLowerInvariant() switch
        {
            Default => DefaultSettings(),
            Relaxed => RelaxedSettings(),
            Strict => StrictSettings(),
            Disabled => DisabledSettings(),
            _ => null,
        };

        if (template is null)
        {
            updated = current;
            return false;
        }

        updated = CopyModerationSettings(current, template);
        return true;
    }

    private static ChatSettings DefaultSettings() => new();

    private static ChatSettings RelaxedSettings() => new()
    {
        RequireReasonForMute = false,
        RequireReasonForWarn = false,
        RequireReasonForBan = true,
        RequireReasonForKick = false,
        WarningLimit = 5,
        WarningLimitAction = ModerationAction.Mute,
        WarningLimitMuteDuration = TimeSpan.FromMinutes(5),
        ModerationEscalation = new ModerationEscalationPolicy
        {
            DeleteThreshold = 8,
            WarningThreshold = 12,
            MuteThreshold = 18,
            BanThreshold = 30,
            MuteDuration = TimeSpan.FromMinutes(5),
        },
        FloodPolicy = new FloodPolicy
        {
            Window = TimeSpan.FromSeconds(15),
            MaximumMessages = 12,
            DeleteMessages = true,
            MuteDuration = TimeSpan.FromMinutes(5),
        },
        ModerationRules =
        [
            DisabledRule("duplicate_message", ModerationRuleType.DuplicateMessage),
            DisabledRule("caps", ModerationRuleType.Caps),
        ],
    };

    private static ChatSettings StrictSettings() => new()
    {
        CaptchaEnabled = true,
        RequireReasonForMute = true,
        RequireReasonForWarn = true,
        RequireReasonForBan = true,
        RequireReasonForKick = true,
        DeleteCommandMessages = true,
        DeleteServiceMessages = true,
        WarningLimit = 2,
        WarningLimitAction = ModerationAction.Mute,
        WarningLimitMuteDuration = TimeSpan.FromMinutes(30),
        ModerationEscalation = new ModerationEscalationPolicy
        {
            DeleteThreshold = 3,
            WarningThreshold = 5,
            MuteThreshold = 8,
            BanThreshold = 14,
            MuteDuration = TimeSpan.FromMinutes(30),
        },
        CaptchaPolicy = new CaptchaPolicy
        {
            Enabled = true,
            Timeout = TimeSpan.FromMinutes(2),
            MaximumAttempts = 2,
            FailureAction = CaptchaFailureAction.Kick,
            DeleteChallengeAfterCompletion = true,
        },
        FloodPolicy = new FloodPolicy
        {
            Window = TimeSpan.FromSeconds(10),
            MaximumMessages = 4,
            DeleteMessages = true,
            MuteDuration = TimeSpan.FromMinutes(30),
        },
        LinkPolicy = new LinkPolicy { Mode = LinkPolicyMode.DenyAll },
        MentionSpamPolicy = new MentionSpamPolicy { Enabled = true, MaximumMentions = 3 },
        ForwardedMessagePolicy = new ForwardedMessagePolicy { Enabled = true, Score = 7 },
        NewMemberPolicy = new NewMemberPolicy
        {
            Enabled = true,
            Window = TimeSpan.FromMinutes(30),
            Score = 4,
        },
        CommandSpamPolicy = new CommandSpamPolicy
        {
            Enabled = true,
            Window = TimeSpan.FromSeconds(30),
            MaximumCommands = 3,
            Score = 7,
        },
    };

    private static ChatSettings DisabledSettings() => new()
    {
        ManualModerationEnabled = false,
        AutoModerationEnabled = false,
        CaptchaEnabled = false,
        CaptchaPolicy = new CaptchaPolicy { Enabled = false },
    };

    private static ModerationRuleDefinition DisabledRule(string id, ModerationRuleType type) => new()
    {
        Id = new RuleId(id),
        Type = type,
        IsEnabled = false,
    };

    private static ChatSettings CopyModerationSettings(ChatSettings current, ChatSettings template) => current with
    {
        ManualModerationEnabled = template.ManualModerationEnabled,
        AutoModerationEnabled = template.AutoModerationEnabled,
        SilentModeration = template.SilentModeration,
        RequireReasonForMute = template.RequireReasonForMute,
        RequireReasonForWarn = template.RequireReasonForWarn,
        RequireReasonForBan = template.RequireReasonForBan,
        RequireReasonForKick = template.RequireReasonForKick,
        CaptchaEnabled = template.CaptchaEnabled,
        DeleteCommandMessages = template.DeleteCommandMessages,
        DeleteServiceMessages = template.DeleteServiceMessages,
        WarningLimit = template.WarningLimit,
        WarningLimitAction = template.WarningLimitAction,
        WarningLimitMuteDuration = template.WarningLimitMuteDuration,
        ModerationEscalation = template.ModerationEscalation,
        ModerationRules = template.ModerationRules,
        CaptchaPolicy = template.CaptchaPolicy,
        FloodPolicy = template.FloodPolicy,
        LinkPolicy = template.LinkPolicy,
        ForbiddenWordsPolicy = template.ForbiddenWordsPolicy,
        MentionSpamPolicy = template.MentionSpamPolicy,
        ForwardedMessagePolicy = template.ForwardedMessagePolicy,
        MediaTypePolicy = template.MediaTypePolicy,
        NewMemberPolicy = template.NewMemberPolicy,
        CommandSpamPolicy = template.CommandSpamPolicy,
    };
}
