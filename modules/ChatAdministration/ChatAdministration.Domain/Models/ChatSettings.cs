namespace ChatAdministration.Domain.Models;

public sealed record ChatSettings
{
    public string Language { get; init; } = "ru";
    public string TimeZone { get; init; } = "UTC";
    public bool ManualModerationEnabled { get; init; } = true;
    public bool AutoModerationEnabled { get; init; } = true;
    public bool SilentModeration { get; init; }
    public bool RequireReasonForMute { get; init; }
    public bool RequireReasonForWarn { get; init; }
    public bool RequireReasonForBan { get; init; } = true;
    public bool RequireReasonForKick { get; init; }
    public bool WelcomeEnabled { get; init; }
    public bool GoodbyeEnabled { get; init; }
    public bool CaptchaEnabled { get; init; }
    public bool DeleteCommandMessages { get; init; }
    public bool DeleteServiceMessages { get; init; }
    public string WelcomeTemplate { get; init; } = "👋 Добро пожаловать, {user}!";
    public string GoodbyeTemplate { get; init; } = "👋 {user} покинул чат.";
    public string RulesText { get; init; } = "Правила чата пока не настроены.";
    public int WarningLimit { get; init; } = 3;
    public ModerationAction WarningLimitAction { get; init; } = ModerationAction.Mute;
    public TimeSpan? WarningLimitMuteDuration { get; init; }
    public ModerationEscalationPolicy ModerationEscalation { get; init; } = new();
    public ChatId? ModerationLogChatId { get; init; }
    public MessageThreadId? ModerationLogThreadId { get; init; }
    public IReadOnlyCollection<ModerationRuleDefinition> ModerationRules { get; init; } = [];
    public IReadOnlyCollection<CustomRoleDefinition> CustomRoles { get; init; } = [];
    public RetentionPolicy RetentionPolicy { get; init; } = new();
    public CaptchaPolicy CaptchaPolicy { get; init; } = new();
    public FloodPolicy FloodPolicy { get; init; } = new();
    public LinkPolicy LinkPolicy { get; init; } = new();
    public ForbiddenWordsPolicy ForbiddenWordsPolicy { get; init; } = new();
    public MentionSpamPolicy MentionSpamPolicy { get; init; } = new();
    public ForwardedMessagePolicy ForwardedMessagePolicy { get; init; } = new();
    public MediaTypePolicy MediaTypePolicy { get; init; } = new();
    public NewMemberPolicy NewMemberPolicy { get; init; } = new();
    public CommandSpamPolicy CommandSpamPolicy { get; init; } = new();
}
