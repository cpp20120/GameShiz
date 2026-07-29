namespace ChatAdministration.Domain.Models;

public enum ModerationRuleType
{
    Flood,
    DuplicateMessage,
    Link,
    MentionSpam,
    Caps,
    ForbiddenWords,
    ForwardedMessage,
    MediaType,
    NewMember,
    CommandSpam,
}
